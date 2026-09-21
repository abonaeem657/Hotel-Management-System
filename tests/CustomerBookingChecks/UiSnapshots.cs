using System.Net;
using System.Text.RegularExpressions;
using hotel.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Optional presentation artifacts from the existing disposable test application.
// No live hotel database, credentials, or Identity cookies are exported.
internal static class UiSnapshots
{
    public static async Task SaveAsync(IServiceProvider services, HttpClient guest, HttpClient customer,
        HttpClient admin, int roomId, int seasonId, int offerId, int itemId, int messageId)
    {
        var directory = Environment.GetEnvironmentVariable("HOTEL_UI_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var webRoot = services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var galleryId = await db.Rooms.Where(r => r.RoomImages.Count > 1).Select(r => r.Id).FirstAsync();
        var serviceId = await db.RoomServices.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();
        var pages = new (HttpClient Client, string Path, string Name)[]
        {
            (guest, "/", "home"), (guest, "/CustomerRooms", "catalog"),
            (guest, $"/CustomerRooms/Details/{roomId}", "room-details"), (guest, "/Contact", "contact"),
            (guest, "/Account/Login", "login"), (guest, "/Account/Register", "register"),
            (customer, "/Bookings/MyBookings", "my-bookings"), (admin, "/Admin", "dashboard"),
            (admin, "/Rooms", "rooms"), (admin, "/RoomServices", "services"),
            (admin, "/Bookings", "bookings"), (admin, "/SeasonPricing", "seasons"),
            (admin, $"/SeasonPricing/Edit/{seasonId}", "season-edit"), (admin, "/Offers", "offers"),
            (admin, $"/Offers/Edit/{offerId}", "offer-edit"), (admin, "/Inventory", "inventory"),
            (admin, $"/Inventory/UpdateQuantity/{itemId}", "quantity"), (admin, "/Inbox", "inbox"),
            (admin, $"/Inbox/Details/{messageId}", "message"),
            (customer, "/Account/AccessDenied", "access-denied"),
            (guest, $"/CustomerRooms/Details/{galleryId}", "gallery"),
            (admin, "/Rooms/Create", "room-create"), (admin, $"/Rooms/Edit/{roomId}", "room-edit"),
            (admin, $"/Rooms/Details/{galleryId}", "room-admin-details"),
            (admin, $"/Rooms/ManageServices/{roomId}", "room-services"),
            (admin, $"/Rooms/ManageImages/{galleryId}", "room-images"),
            (admin, $"/Rooms/Delete/{roomId}", "room-delete"),
            (admin, "/RoomServices/Create", "service-create"),
            (admin, $"/RoomServices/Edit/{serviceId}", "service-edit"),
            (admin, $"/RoomServices/Delete/{serviceId}", "service-delete"),
            (admin, "/SeasonPricing/Create", "season-create"),
            (admin, $"/SeasonPricing/Delete/{seasonId}", "season-delete"),
            (admin, "/Offers/Create", "offer-create"), (admin, $"/Offers/Delete/{offerId}", "offer-delete"),
            (admin, "/Inventory/Create", "inventory-create"),
            (admin, $"/Inventory/Edit/{itemId}", "inventory-edit"),
            (admin, $"/Inventory/Delete/{itemId}", "inventory-delete"),
            (admin, $"/Inbox/Delete/{messageId}", "message-delete")
        };
        foreach (var page in pages)
        {
            using var response = await page.Client.GetAsync(page.Path);
            if (page.Name == "access-denied")
            {
                if (response.StatusCode != HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("Access Denied snapshot must retain HTTP 403.");
            }
            else response.EnsureSuccessStatusCode();
            await SaveHtml(page.Name, await response.Content.ReadAsStringAsync());
        }

        // Use the real preview POST and existing fixture room without creating bookings.
        var details = await customer.GetStringAsync($"/CustomerRooms/Details/{roomId}");
        var token = WebUtility.HtmlDecode(Regex.Match(details,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        foreach (var valid in new[] { true, false })
        {
            using var response = await customer.PostAsync("/CustomerRooms/Preview", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["Booking.RoomId"] = roomId.ToString(),
                ["Booking.CheckInDate"] = valid ? DateTime.Today.AddDays(130).ToString("yyyy-MM-dd") : "",
                ["Booking.CheckOutDate"] = valid ? DateTime.Today.AddDays(134).ToString("yyyy-MM-dd") : ""
            }));
            response.EnsureSuccessStatusCode();
            await SaveHtml(valid ? "price-preview" : "booking-validation", await response.Content.ReadAsStringAsync());
        }

        async Task SaveHtml(string name, string html)
        {
            html = Regex.Replace(html, "(?<prefix>(?:href|src)=\")/(?<asset>(?:css|lib|js|images)/[^\"]+)", match =>
                match.Groups["prefix"].Value + new Uri(Path.Combine(webRoot, match.Groups["asset"].Value.Split('?')[0])).AbsoluteUri);
            // Tokens are unnecessary for offline presentation review.
            html = Regex.Replace(html, "<input name=\"__RequestVerificationToken\"[^>]+>", "");
            // These existing gallery fixture paths are deliberately not hotel photographs.
            html = Regex.Replace(html, "/test-photo-([12])\\.jpg", match =>
                "data:image/svg+xml," + Uri.EscapeDataString(
                    $"<svg xmlns='http://www.w3.org/2000/svg' width='800' height='500'><rect width='800' height='500' fill='{(match.Groups[1].Value == "1" ? "#dfe7e9" : "#e5dcc7")}'/><text x='80' y='250' font-size='40'>Test image {match.Groups[1].Value}</text></svg>"));
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), html);
        }
    }
}
