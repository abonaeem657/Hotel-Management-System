using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using hotel.Controllers;
using hotel.Data;
using hotel.Models;
using hotel.Services;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ManagementChecks
{
    public static async Task RunAsync(ApplicationDbContext db, IServiceProvider services, HttpClient guest,
        HttpClient customer, HttpClient admin, string userId, int roomId, DateTime start, Action<bool, string> check)
    {
        check(!db.Database.HasPendingModelChanges(), "Existing migration snapshot matches the model; no schema change required");
        async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string>? values = null)
        {
            var html = await client.GetStringAsync("/Contact");
            values ??= new();
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html,
                "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
            return await client.PostAsync(path, new FormUrlEncodedContent(values));
        }
        async Task<Booking> ReadBooking(int id) => await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == id);
        bool Redirect(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Redirect;

        var controllers = typeof(BookingsController).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Controller)));
        foreach (var type in controllers)
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsDefined(typeof(HttpPostAttribute))))
            check(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), $"Anti-forgery declared: {type.Name}.{method.Name}");

        foreach (var path in new[] { "/Admin", "/Admin/Dashboard", "/Bookings", "/SeasonPricing", "/Offers", "/Inventory", "/Inbox" })
        {
            check(Redirect(await guest.GetAsync(path)), "Anonymous management access challenged: " + path);
            var denied = await customer.GetAsync(path);
            check(Redirect(denied) && denied.Headers.Location!.ToString().Contains("AccessDenied"), "Customer management access denied: " + path);
            var response = await admin.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.OK || path == "/Admin/Dashboard" && Redirect(response), "Admin page available: " + path);
        }
        foreach (var path in new[] { "/Bookings/Approve/1", "/Bookings/Reject/1", "/Bookings/Complete/1",
            "/SeasonPricing/Create", "/Offers/Create", "/Inventory/Create", "/Inventory/UpdateQuantity/1", "/Inbox/MarkAsRead/1", "/Inbox/DeleteConfirmed/1" })
        {
            var denied = await Post(customer, path);
            check(Redirect(denied) && denied.Headers.Location!.ToString().Contains("AccessDenied"), "Customer management POST denied: " + path);
            check((await admin.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
                "Management POST requires token: " + path);
        }
        check((await admin.GetAsync("/Bookings/MyBookings")).Headers.Location!.ToString().Contains("AccessDenied"), "My Bookings requires Customer role");
        check((await admin.GetAsync("/Bookings?status=999")).StatusCode == HttpStatusCode.BadRequest, "Undefined booking filter rejected");
        check((await admin.GetAsync("/Bookings?status=invalid")).StatusCode == HttpStatusCode.BadRequest, "Malformed booking filter rejected");

        var lifecycle = new Booking { RoomId = roomId, UserId = userId, CheckInDate = start.AddDays(50),
            CheckOutDate = start.AddDays(52), TotalAmount = 200m };
        db.Bookings.Add(lifecycle);
        await db.SaveChangesAsync();
        await Post(admin, $"/Bookings/Complete/{lifecycle.Id}");
        check((await ReadBooking(lifecycle.Id)).BookingStatus == BookingStatus.Pending, "Pending cannot complete");
        await Post(admin, $"/Bookings/Reject/{lifecycle.Id}", new() { ["rejectionReason"] = "   " });
        check((await ReadBooking(lifecycle.Id)).BookingStatus == BookingStatus.Pending
            && (await admin.GetStringAsync("/Bookings")).Contains("rejection reason is required"), "Blank rejection reason rejected with feedback");
        await Post(admin, $"/Bookings/Approve/{lifecycle.Id}");
        check((await ReadBooking(lifecycle.Id)).BookingStatus == BookingStatus.Approved, "Pending approved by Admin");
        check((await db.Rooms.AsNoTracking().SingleAsync(r => r.Id == roomId)).IsAvailable, "Future approval preserves general availability");
        await Post(admin, $"/Bookings/Reject/{lifecycle.Id}", new() { ["rejectionReason"] = "Not allowed" });
        check((await ReadBooking(lifecycle.Id)).BookingStatus == BookingStatus.Approved, "Approved cannot reject");
        await Post(admin, $"/Bookings/Complete/{lifecycle.Id}");
        await Post(admin, $"/Bookings/Approve/{lifecycle.Id}");
        check((await ReadBooking(lifecycle.Id)).BookingStatus == BookingStatus.Completed, "Approved completes; Completed cannot reapprove");

        var rejected = new Booking { RoomId = roomId, UserId = userId, CheckInDate = start.AddDays(60), CheckOutDate = start.AddDays(62), TotalAmount = 200 };
        db.Bookings.Add(rejected);
        await db.SaveChangesAsync();
        await Post(admin, $"/Bookings/Reject/{rejected.Id}", new() { ["rejectionReason"] = "  Maintenance required  " });
        await Post(admin, $"/Bookings/Approve/{rejected.Id}");
        var rejectedSaved = await ReadBooking(rejected.Id);
        check(rejectedSaved.BookingStatus == BookingStatus.Rejected && rejectedSaved.RejectionReason == "Maintenance required", "Reject persists trimmed reason; rejected cannot reapprove");
        check((await customer.GetStringAsync("/Bookings/MyBookings")).Contains("Maintenance required"), "Customer sees Admin rejection reason");
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            using var scope = services.CreateScope();
            var controller = ActivatorUtilities.CreateInstance<BookingsController>(scope.ServiceProvider);
            var result = (ViewResult)await controller.Index(status);
            var model = (AdminBookingsViewModel)result.Model!;
            check(model.Bookings.All(b => b.BookingStatus == status), "Booking filter returns only " + status);
        }
        var conflictA = new Booking { RoomId = roomId, UserId = userId, CheckInDate = start.AddDays(70), CheckOutDate = start.AddDays(72), TotalAmount = 200 };
        var conflictB = new Booking { RoomId = roomId, UserId = userId, CheckInDate = start.AddDays(71), CheckOutDate = start.AddDays(73), TotalAmount = 200 };
        db.Bookings.AddRange(conflictA, conflictB);
        await db.SaveChangesAsync();
        // Independent scopes avoid sharing DbContexts and exercise room locks concurrently.
        async Task<BookingTransitionResult> Approve(int id)
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AdminBookingService>().TransitionAsync(id, BookingStatus.Approved);
        }
        var concurrent = await Task.WhenAll(Approve(conflictA.Id), Approve(conflictB.Id));
        check(concurrent.Count(r => r.Error == null) == 1 && concurrent.Any(r => r.Error?.Contains("overlaps") == true), "Concurrent conflicting approvals allow exactly one");
        var blocked = (await ReadBooking(conflictA.Id)).BookingStatus == BookingStatus.Pending ? conflictA : conflictB;
        await Post(admin, $"/Bookings/Approve/{blocked.Id}");
        check((await admin.GetStringAsync("/Bookings")).Contains("overlaps these dates"), "Approval conflict displays understandable error");
        var approved = blocked == conflictA ? conflictB : conflictA;
        var adjacent = new Booking { RoomId = roomId, UserId = userId, CheckInDate = approved.CheckOutDate, CheckOutDate = approved.CheckOutDate.AddDays(1), TotalAmount = 100 };
        db.Bookings.Add(adjacent);
        await db.SaveChangesAsync();
        await Post(admin, $"/Bookings/Approve/{adjacent.Id}");
        check((await ReadBooking(adjacent.Id)).BookingStatus == BookingStatus.Approved, "Adjacent Admin approval allowed");
        check((await Post(admin, "/Bookings/Approve/2147483647")).StatusCode == HttpStatusCode.NotFound, "Unknown booking cannot be approved");

        var roomCount = await db.Rooms.CountAsync();
        await Post(admin, "/Rooms/Create", new() { ["RoomNumber"] = "INVALID", ["RoomType"] = "Test", ["BasePricePerNight"] = "-1" });
        check(await db.Rooms.CountAsync() == roomCount, "Negative room price rejected server-side");
        var bookingCount = await db.Bookings.CountAsync();
        var deleteRoom = await Post(admin, $"/Rooms/DeleteConfirmed/{roomId}");
        check((await deleteRoom.Content.ReadAsStringAsync()).Contains("booking history") && await db.Bookings.CountAsync() == bookingCount,
            "Room deletion preserves booking history and shows error on standalone page");
        check((await Post(admin, $"/Rooms/DeleteConfirmed/{roomId}", new() { ["modalRequest"] = "true" })).StatusCode == HttpStatusCode.BadRequest,
            "Room deletion preserves booking history in modal");
        await Post(admin, "/RoomServices/Create", new() { ["ServiceName"] = "Secure service", ["Rooms[0].RoomNumber"] = "INJECTED", ["Rooms[0].RoomType"] = "Test", ["Rooms[0].BasePricePerNight"] = "1" });
        check(await db.RoomServices.AnyAsync(s => s.ServiceName == "Secure service") && await db.Rooms.CountAsync() == roomCount, "Room Service ignores posted navigation graph");
        var secureService = await db.RoomServices.AsNoTracking().SingleAsync(s => s.ServiceName == "Secure service");
        await Post(admin, $"/RoomServices/Edit/{secureService.Id}", new() { ["Id"] = secureService.Id.ToString(), ["ServiceName"] = "Updated secure service", ["Rooms[0].RoomNumber"] = "INJECTED" });
        check(await db.RoomServices.AnyAsync(s => s.Id == secureService.Id && s.ServiceName == "Updated secure service")
            && await db.Rooms.CountAsync() == roomCount, "Room Service edit changes only name");

        await Post(admin, "/Rooms/Create", new() { ["RoomNumber"] = "UPLOAD-CHECK", ["RoomType"] = "Test", ["BasePricePerNight"] = "50", ["IsAvailable"] = "true" });
        var uploadRoom = await db.Rooms.AsNoTracking().SingleAsync(r => r.RoomNumber == "UPLOAD-CHECK");
        await Post(admin, "/Rooms/ManageServices", new() { ["RoomId"] = uploadRoom.Id.ToString(), ["SelectedServiceIds"] = secureService.Id.ToString() });
        check(await db.Rooms.AnyAsync(r => r.Id == uploadRoom.Id && r.RoomServices.Any(s => s.Id == secureService.Id)), "Room services can be assigned");
        var tokenPage = await admin.GetStringAsync("/Contact");
        var token = WebUtility.HtmlDecode(Regex.Match(tokenPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(token), "__RequestVerificationToken");
            form.Add(new StringContent(uploadRoom.Id.ToString()), "roomId");
            var image = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j5WQAAAAASUVORK5CYII="));
            image.Headers.ContentType = new("image/png");
            form.Add(image, "imageFile", "test.png");
            check(Redirect(await admin.PostAsync("/Rooms/UploadImage", form)), "Room image uploads through multipart form");
        }
        var uploaded = await db.RoomImages.AsNoTracking().SingleAsync(i => i.RoomId == uploadRoom.Id);
        check((await guest.GetAsync(uploaded.ImagePath)).StatusCode == HttpStatusCode.OK, "Uploaded image is served from configured web root");
        await Post(admin, "/Rooms/DeleteImage", new() { ["roomId"] = uploadRoom.Id.ToString(), ["imageId"] = uploaded.Id.ToString() });
        check(!await db.RoomImages.AnyAsync(i => i.Id == uploaded.Id) && (await guest.GetAsync(uploaded.ImagePath)).StatusCode == HttpStatusCode.NotFound,
            "Image deletion removes database record and file");
        check(Redirect(await Post(admin, $"/Rooms/DeleteConfirmed/{uploadRoom.Id}")) && !await db.Rooms.AnyAsync(r => r.Id == uploadRoom.Id), "Room without bookings can still be deleted");
        check(Redirect(await Post(admin, $"/RoomServices/DeleteConfirmed/{secureService.Id}")), "Room Service deletion remains functional");

        var seasonCount = await db.SeasonPricings.CountAsync();
        Dictionary<string, string> Season(string multiplier = "1.25", string? end = null) => new()
        {
            ["SeasonName"] = "Managed season", ["StartDate"] = start.AddDays(100).ToString("yyyy-MM-dd"),
            ["EndDate"] = end ?? start.AddDays(102).ToString("yyyy-MM-dd"), ["PriceMultiplier"] = multiplier
        };
        foreach (var invalid in new[] { Season("0"), Season("-1"), Season("0.001"), Season(end: start.ToString("yyyy-MM-dd")), new Dictionary<string, string>() })
            await Post(admin, "/SeasonPricing/Create", invalid);
        check(await db.SeasonPricings.CountAsync() == seasonCount, "Season rejects missing fields, invalid range and nonpositive/unrepresentable multiplier");
        check(Redirect(await Post(admin, "/SeasonPricing/Create", Season())), "Admin creates season");
        var season = await db.SeasonPricings.AsNoTracking().SingleAsync(s => s.SeasonName == "Managed season");
        check((await admin.GetAsync($"/SeasonPricing/Edit/{season.Id}")).StatusCode == HttpStatusCode.OK, "Season edit form renders");
        await Post(admin, $"/SeasonPricing/Edit/{season.Id}", Season("1.5"));
        check((await db.SeasonPricings.AsNoTracking().SingleAsync(s => s.Id == season.Id)).PriceMultiplier == 1.5m, "Admin edits season");

        var offerCount = await db.Offers.CountAsync();
        foreach (var invalid in new[] { ("0", "10"), ("1", "-1"), ("1", "101") })
            await Post(admin, "/Offers/Create", new() { ["MinimumNights"] = invalid.Item1, ["DiscountPercentage"] = invalid.Item2 });
        check(await db.Offers.CountAsync() == offerCount, "Offers reject invalid nights and discount");
        await Post(admin, "/Offers/Create", new() { ["MinimumNights"] = "1", ["DiscountPercentage"] = "25" });
        var offer = await db.Offers.AsNoTracking().SingleAsync(o => o.DiscountPercentage == 25);
        check((await admin.GetAsync($"/Offers/Edit/{offer.Id}")).StatusCode == HttpStatusCode.OK, "Offer edit form renders");
        await Post(admin, $"/Offers/Edit/{offer.Id}", new() { ["MinimumNights"] = "1", ["DiscountPercentage"] = "30" });
        using (var scope = services.CreateScope())
        {
            var pricing = scope.ServiceProvider.GetRequiredService<BookingPricingService>();
            check((await pricing.CalculateAsync(100, start.AddDays(100), start.AddDays(101))).TotalAmount == 105m,
                "Managed season and offer immediately affect existing pricing service");
        }
        foreach (var boundary in new[] { "0", "100" })
        {
            check(Redirect(await Post(admin, $"/Offers/Edit/{offer.Id}", new() { ["MinimumNights"] = "400", ["DiscountPercentage"] = boundary })), "Offer supports discount boundary " + boundary);
        }

        var inventoryCount = await db.Inventories.CountAsync();
        await Post(admin, "/Inventory/Create", new() { ["ItemName"] = "Invalid", ["CurrentQuantity"] = "-1", ["ReorderLevel"] = "0" });
        await Post(admin, "/Inventory/Create", new() { ["ItemName"] = "Invalid", ["CurrentQuantity"] = "0", ["ReorderLevel"] = "-1" });
        check(await db.Inventories.CountAsync() == inventoryCount, "Negative inventory values rejected");
        await Post(admin, "/Inventory/Create", new() { ["ItemName"] = "Towels", ["CurrentQuantity"] = "5", ["ReorderLevel"] = "5" });
        var item = await db.Inventories.AsNoTracking().SingleAsync(i => i.ItemName == "Towels");
        check((await admin.GetStringAsync("/Inventory")).Contains("Low Stock"), "Quantity equal to reorder level is Low Stock");
        check((await admin.GetAsync($"/Inventory/Edit/{item.Id}")).StatusCode == HttpStatusCode.OK, "Inventory edit form renders");
        await Post(admin, $"/Inventory/Edit/{item.Id}", new() { ["ItemName"] = "Clean towels", ["CurrentQuantity"] = "5", ["ReorderLevel"] = "4" });
        check((await admin.GetStringAsync("/Inventory")).Contains("In Stock"), "Quantity above reorder level is In Stock");
        check((await admin.GetAsync($"/Inventory/UpdateQuantity/{item.Id}")).StatusCode == HttpStatusCode.OK, "Quantity form renders");
        await Post(admin, $"/Inventory/UpdateQuantity/{item.Id}", new() { ["CurrentQuantity"] = "-1" });
        check((await db.Inventories.AsNoTracking().SingleAsync(i => i.Id == item.Id)).CurrentQuantity == 5, "Negative quantity update rejected");
        await Post(admin, $"/Inventory/UpdateQuantity/{item.Id}", new() { ["CurrentQuantity"] = "0", ["ReorderLevel"] = "999", ["ItemName"] = "Tampered" });
        var updatedItem = await db.Inventories.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        check(updatedItem.CurrentQuantity == 0 && updatedItem.ReorderLevel == 4 && updatedItem.ItemName == "Clean towels", "Quantity updates only authorized field");

        check((await guest.GetAsync("/Contact")).StatusCode == HttpStatusCode.OK, "Contact is public");
        check((await guest.PostAsync("/Contact", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest, "Contact requires anti-forgery");
        await Post(guest, "/Contact", new() { ["SenderName"] = "", ["Email"] = "invalid", ["MessageBody"] = "" });
        check(!await db.ContactMessages.AnyAsync(), "Contact validates required fields and email");
        var before = DateTime.UtcNow.AddSeconds(-1);
        await Post(guest, "/Contact", new() { ["SenderName"] = "Visitor", ["Email"] = "visitor@example.test", ["MessageBody"] = "<script>alert(1)</script>", ["IsRead"] = "true", ["SentDate"] = "2000-01-01" });
        var message = await db.ContactMessages.AsNoTracking().SingleAsync();
        check(!message.IsRead && message.SentDate >= before, "Contact ignores tampered read flag and date");
        check((await guest.GetStringAsync("/Contact")).Contains("Your message has been sent"), "Contact confirms successful submission");
        var detail = await admin.GetStringAsync($"/Inbox/Details/{message.Id}");
        check(detail.Contains("&lt;script&gt;") && !detail.Contains("<script>alert(1)</script>"), "Inbox encodes user content");
        check(!(await db.ContactMessages.AsNoTracking().SingleAsync()).IsRead, "Viewing message uses read-only GET");
        await Post(admin, $"/Inbox/MarkAsRead/{message.Id}");
        check((await db.ContactMessages.AsNoTracking().SingleAsync()).IsRead, "Admin marks message read");
        using (var scope = services.CreateScope())
        {
            var controller = ActivatorUtilities.CreateInstance<AdminController>(scope.ServiceProvider);
            var model = (AdminDashboardViewModel)((ViewResult)await controller.Index()).Model!;
            check(model.TotalRooms == await db.Rooms.CountAsync() && model.TotalBookings == await db.Bookings.CountAsync()
                && model.TotalCustomers == 3 && model.LowStockItems == 1 && model.UnreadMessages == 0
                && model.CompletedRevenue == await db.Bookings.Where(b => b.BookingStatus == BookingStatus.Completed).SumAsync(b => b.TotalAmount),
                "Dashboard uses database counts and Completed-only revenue");
        }

        await UiSnapshots.SaveAsync(services, guest, customer, admin, roomId, season.Id, offer.Id, item.Id, message.Id);

        foreach (var record in new[] { ("SeasonPricing", season.Id), ("Offers", offer.Id), ("Inventory", item.Id), ("Inbox", message.Id) })
        {
            check((await admin.GetAsync($"/{record.Item1}/Delete/{record.Id}")).StatusCode == HttpStatusCode.OK, "Delete confirmation renders: " + record.Item1);
            check(Redirect(await Post(admin, $"/{record.Item1}/DeleteConfirmed/{record.Id}")), "Admin deletes: " + record.Item1);
            check((await Post(admin, $"/{record.Item1}/DeleteConfirmed/{record.Id}")).StatusCode == HttpStatusCode.NotFound, "Repeated delete returns 404: " + record.Item1);
        }
        foreach (var path in new[] { "/SeasonPricing/Edit/2147483647", "/Offers/Edit/2147483647", "/Inventory/Edit/2147483647", "/Inventory/UpdateQuantity/2147483647", "/Inbox/Details/2147483647" })
            check((await admin.GetAsync(path)).StatusCode == HttpStatusCode.NotFound, "Missing record returns 404: " + path);

        // Final empty-state checks run only in this harness's disposable database.
        await db.Bookings.ExecuteDeleteAsync();
        await db.Rooms.ExecuteDeleteAsync();
        await db.RoomServices.ExecuteDeleteAsync();
        await db.SeasonPricings.ExecuteDeleteAsync();
        await db.Offers.ExecuteDeleteAsync();
        await db.Inventories.ExecuteDeleteAsync();
        await db.ContactMessages.ExecuteDeleteAsync();
        foreach (var path in new[] { "/Admin", "/Rooms", "/RoomServices", "/Bookings", "/SeasonPricing", "/Offers", "/Inventory", "/Inbox" })
            check((await admin.GetAsync(path)).StatusCode == HttpStatusCode.OK, "Empty management page renders: " + path);
        check((await guest.GetAsync("/CustomerRooms")).StatusCode == HttpStatusCode.OK && (await customer.GetAsync("/Bookings/MyBookings")).StatusCode == HttpStatusCode.OK,
            "Empty customer pages render");
    }
}
