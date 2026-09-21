using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using hotel.Controllers;
using hotel.Data;
using hotel.Models;
using hotel.Services;
using hotel.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var projectPath = Path.GetFullPath(args.Length > 0 ? args[0] : "hotel");
var config = new ConfigurationBuilder().SetBasePath(projectPath).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true).AddEnvironmentVariables().Build();
var connection = new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"))
{
    InitialCatalog = "HotelCustomerChecks_" + Guid.NewGuid().ToString("N")
};
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options;
await using var database = new ApplicationDbContext(options);
var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}

var start = DateTime.Today.AddDays(30);
var seasons = new[]
{
    new SeasonPricing { SeasonName = "Peak", StartDate = start.AddDays(1), EndDate = start.AddDays(2), PriceMultiplier = 2m },
    new SeasonPricing { SeasonName = "Special", StartDate = start.AddDays(2), EndDate = start.AddDays(2), PriceMultiplier = 3m }
};
var offers = new[]
{
    new Offer { MinimumNights = 2, DiscountPercentage = 10 },
    new Offer { MinimumNights = 4, DiscountPercentage = 20 },
    new Offer { MinimumNights = 5, DiscountPercentage = 50 }
};
var price = BookingPricingService.Calculate(100m, start, start.AddDays(4), seasons, offers);
Check(price.Nights == 4 && price.BaseSubtotal == 400m && price.SeasonalAdjustment == 300m
    && price.DiscountAmount == 140m && price.TotalAmount == 560m, "Spanning seasons, inclusive end, highest multiplier, best qualifying offer");
Check(BookingPricingService.Calculate(100m, start.AddDays(10), start.AddDays(11), seasons, offers).TotalAmount == 100m,
    "Unseasoned night without eligible offer");
Check(BookingPricingService.Calculate(100m, start, start.AddDays(1),
    new[] { new SeasonPricing { StartDate = start, EndDate = start, PriceMultiplier = .5m } }, Array.Empty<Offer>()).TotalAmount == 50m,
    "Season multiplier below one");
Check(BookingPricingService.Calculate(99.99m, start, start.AddDays(1), Array.Empty<SeasonPricing>(),
    new[] { new Offer { MinimumNights = 1, DiscountPercentage = 10m } }).TotalAmount == 89.99m, "Two-decimal price rounding");
foreach (var dates in new[]
{
    new BookingRequestViewModel { RoomId = 1 },
    new BookingRequestViewModel { RoomId = 1, CheckInDate = DateTime.Today.AddDays(-1), CheckOutDate = DateTime.Today.AddDays(1) },
    new BookingRequestViewModel { RoomId = 1, CheckInDate = start, CheckOutDate = start }
})
    Check(!Validator.TryValidateObject(dates, new ValidationContext(dates), new List<ValidationResult>(), true), "Invalid dates rejected by server model");

WebApplication? app = null;
try
{
    // Only this newly named, disposable database is created. No migrations or live schema changes.
    await database.Database.EnsureCreatedAsync();
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ApplicationName = typeof(CustomerRoomsController).Assembly.GetName().Name,
        ContentRootPath = projectPath,
        WebRootPath = Path.Combine(projectPath, "wwwroot"),
        EnvironmentName = "Development"
    });
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection.ConnectionString));
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o => o.User.RequireUniqueEmail = true)
        .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
    });
    builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CustomerRoomsController).Assembly);
    builder.Services.AddScoped<CustomerBookingService>();
    builder.Services.AddScoped<BookingPricingService>();
    builder.Services.AddScoped<AdminBookingService>();
    app = builder.Build();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

    var emptyConfiguration = new ConfigurationBuilder().Build();
    await IdentitySeeder.SeedAsync(app.Services, emptyConfiguration, true);
    await IdentitySeeder.SeedAsync(app.Services, emptyConfiguration, true);
    Check(await database.Roles.CountAsync() == 2 && !await database.Users.AnyAsync(), "Roles seed idempotently; missing Admin credentials skip account creation");
    var testPassword = "aA1!" + Guid.NewGuid().ToString("N");
    var adminConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DevelopmentAdmin:Email"] = "admin@example.test", ["DevelopmentAdmin:Password"] = testPassword
    }).Build();
    await IdentitySeeder.SeedAsync(app.Services, adminConfiguration, false);
    Check(!await database.Users.AnyAsync(), "Production never seeds development Admin");
    await IdentitySeeder.SeedAsync(app.Services, adminConfiguration, true);
    await IdentitySeeder.SeedAsync(app.Services, adminConfiguration, true);
    Check(await database.Users.CountAsync() == 1 && await database.UserRoles.CountAsync() == 1, "Development Admin seeds once with Admin membership");

    var room = new Room { RoomNumber = "TEST-A", RoomType = "Suite", BasePricePerNight = 100, IsAvailable = true };
    room.RoomServices.Add(new RoomService { ServiceName = "Breakfast" });
    var disabled = new Room { RoomNumber = "TEST-OFF", RoomType = "Closed", BasePricePerNight = 50, IsAvailable = false };
    var gallery = new Room { RoomNumber = "TEST-GALLERY", RoomType = "Double", BasePricePerNight = 150, IsAvailable = true };
    gallery.RoomImages.Add(new RoomImage { ImagePath = "/test-photo-1.jpg" });
    gallery.RoomImages.Add(new RoomImage { ImagePath = "/test-photo-2.jpg" });
    database.Rooms.AddRange(room, disabled, gallery);
    database.SeasonPricings.AddRange(seasons);
    database.Offers.AddRange(offers);
    await database.SaveChangesAsync();

    string firstId;
    string secondId;
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var first = new ApplicationUser { Name = "Test guest A", UserName = "guest-a@example.test", Email = "guest-a@example.test" };
        var second = new ApplicationUser { Name = "Test guest B", UserName = "guest-b@example.test", Email = "guest-b@example.test" };
        Check((await users.CreateAsync(first, testPassword)).Succeeded && (await users.CreateAsync(second, testPassword)).Succeeded,
            "Isolated Identity test users created");
        Check((await users.AddToRoleAsync(first, "Customer")).Succeeded && (await users.AddToRoleAsync(second, "Customer")).Succeeded,
            "Test customers assigned Customer role");
        firstId = first.Id;
        secondId = second.Id;
    }
    await app.StartAsync();
    var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    HttpClient Guest() => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(url) };
    async Task<HttpResponseMessage> AccountPost(HttpClient client, string action, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(action == "Logout" ? "/" : "/Account/" + action);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        return await client.PostAsync("/Account/" + action, new FormUrlEncodedContent(values));
    }
    async Task<HttpClient> SignedIn(string email, bool admin = false)
    {
        var client = Guest();
        var response = await AccountPost(client, "Login", new() { ["Email"] = email, ["Password"] = testPassword });
        Check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.ToString() == (admin ? "/Admin" : "/CustomerRooms"),
            admin ? "Valid Admin login routes to Dashboard" : "Valid Customer login routes to CustomerRooms");
        return client;
    }
    async Task<HttpResponseMessage> Submit(HttpClient client, string action, int roomId, DateTime? checkIn, DateTime? checkOut, bool tamper = false)
    {
        var details = await client.GetStringAsync($"/CustomerRooms/Details/{roomId}");
        var token = WebUtility.HtmlDecode(Regex.Match(details, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
        var values = new Dictionary<string, string>
        {
            ["Booking.RoomId"] = roomId.ToString(),
            ["Booking.CheckInDate"] = checkIn?.ToString("yyyy-MM-dd") ?? "",
            ["Booking.CheckOutDate"] = checkOut?.ToString("yyyy-MM-dd") ?? "",
            ["__RequestVerificationToken"] = token
        };
        if (tamper)
        {
            values["UserId"] = secondId;
            values["Booking.UserId"] = secondId;
            values["Booking.TotalAmount"] = "0.01";
            values["Booking.BookingStatus"] = "Approved";
        }
        return await client.PostAsync(action, new FormUrlEncodedContent(values));
    }
    using var guest = Guest();
    using var firstClient = await SignedIn("guest-a@example.test");
    using var secondClient = await SignedIn("guest-b@example.test");
    using var adminClient = await SignedIn("admin@example.test", admin: true);
    using var registerClient = Guest();
    var register = await AccountPost(registerClient, "Register", new()
    {
        ["Name"] = "New Customer", ["Email"] = "registered@example.test", ["PhoneNumber"] = "+962790000000",
        ["Password"] = testPassword, ["ConfirmPassword"] = testPassword, ["Role"] = "Admin", ["Roles"] = "Admin"
    });
    Check(register.StatusCode == HttpStatusCode.Redirect && register.Headers.Location!.ToString() == "/CustomerRooms", "Registration signs in and redirects to CustomerRooms");
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var registered = (await users.FindByEmailAsync("registered@example.test"))!;
        Check(registered.Name == "New Customer" && registered.PhoneNumber == "+962790000000"
            && await users.IsInRoleAsync(registered, "Customer") && !await users.IsInRoleAsync(registered, "Admin"),
            "Registration stores profile and forces Customer despite submitted Admin role");
    }
    Check((await registerClient.GetAsync("/Bookings/MyBookings")).StatusCode == HttpStatusCode.OK, "Registered customer immediately authenticated");
    var collisionConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DevelopmentAdmin:Email"] = "registered@example.test", ["DevelopmentAdmin:Password"] = testPassword
    }).Build();
    await IdentitySeeder.SeedAsync(app.Services, collisionConfiguration, true);
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Check(!await users.IsInRoleAsync((await users.FindByEmailAsync("registered@example.test"))!, "Admin"), "Seeder never promotes an existing Customer");
    }
    using var invalidClient = Guest();
    var invalidRegister = await AccountPost(invalidClient, "Register", new()
    {
        ["Name"] = "", ["Email"] = "invalid", ["PhoneNumber"] = "invalid", ["Password"] = testPassword, ["ConfirmPassword"] = "different"
    });
    Check(invalidRegister.StatusCode == HttpStatusCode.OK && (await invalidRegister.Content.ReadAsStringAsync()).Contains("validation-summary-errors"),
        "Invalid registration fields show server validation");
    var weakPassword = await AccountPost(invalidClient, "Register", new()
    {
        ["Name"] = "Weak", ["Email"] = "weak@example.test", ["PhoneNumber"] = "+962790000000", ["Password"] = "x", ["ConfirmPassword"] = "x"
    });
    Check(weakPassword.StatusCode == HttpStatusCode.OK && !await database.Users.AnyAsync(u => u.Email == "weak@example.test"), "Identity password policy preserved");
    var failedLogin = await AccountPost(invalidClient, "Login", new() { ["Email"] = "guest-a@example.test", ["Password"] = "wrong" });
    var unknownLogin = await AccountPost(invalidClient, "Login", new() { ["Email"] = "unknown@example.test", ["Password"] = "wrong" });
    Check((await failedLogin.Content.ReadAsStringAsync()).Contains("Invalid email or password.")
        && (await unknownLogin.Content.ReadAsStringAsync()).Contains("Invalid email or password."), "Known and unknown accounts receive generic login failure");
    Check((await guest.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest
        && (await guest.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
        "Login and Register require anti-forgery tokens");
    using var returnClient = Guest();
    var localReturn = $"/CustomerRooms/Details/{room.Id}?checkInDate={start:yyyy-MM-dd}&checkOutDate={start.AddDays(4):yyyy-MM-dd}";
    var loginReturn = await AccountPost(returnClient, "Login", new()
    {
        ["Email"] = "guest-a@example.test", ["Password"] = testPassword, ["RememberMe"] = "true", ["ReturnUrl"] = localReturn
    });
    Check(loginReturn.Headers.Location!.ToString() == localReturn, "Safe ReturnUrl preserves room and dates");
    Check(loginReturn.Headers.GetValues("Set-Cookie").Any(c => c.Contains(".AspNetCore.Identity.Application") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase)),
        "Remember Me issues a persistent Identity cookie");
    foreach (var unsafeUrl in new[] { "https://example.org", "//example.org", "/\\example.org" })
    {
        var unsafeReturn = await AccountPost(returnClient, "Login", new()
        {
            ["Email"] = "guest-a@example.test", ["Password"] = testPassword, ["ReturnUrl"] = unsafeUrl
        });
        Check(unsafeReturn.Headers.Location!.ToString() == "/CustomerRooms", "Unsafe ReturnUrl blocked");
    }
    Check((await registerClient.GetAsync("/Account/Logout")).StatusCode == HttpStatusCode.MethodNotAllowed, "Logout is not available through GET");
    Check((await registerClient.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
        "Logout requires anti-forgery token");
    var logout = await AccountPost(registerClient, "Logout", new());
    Check(logout.StatusCode == HttpStatusCode.Redirect && (await registerClient.GetAsync("/Bookings/MyBookings")).StatusCode == HttpStatusCode.Redirect,
        "POST Logout removes authenticated session");
    foreach (var adminPath in new[] { "/Rooms", "/RoomServices", $"/Rooms/ManageImages/{room.Id}", $"/Rooms/ManageServices/{room.Id}" })
    {
        Check((await guest.GetAsync(adminPath)).StatusCode == HttpStatusCode.Redirect, "Anonymous Admin access challenged: " + adminPath);
        var denied = await firstClient.GetAsync(adminPath);
        Check(denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.ToString().Contains("/Account/AccessDenied"), "Customer Admin access denied: " + adminPath);
        Check((await adminClient.GetAsync(adminPath)).StatusCode == HttpStatusCode.OK, "Admin access allowed: " + adminPath);
    }
    Check((await firstClient.GetAsync("/Account/AccessDenied")).StatusCode == HttpStatusCode.Forbidden, "Access Denied page returns HTTP 403");
    var customerNav = await firstClient.GetStringAsync("/");
    var adminNav = await adminClient.GetStringAsync("/");
    Check(!customerNav.Contains("Rooms Management") && customerNav.Contains("My Bookings") && customerNav.Contains("Logout")
        && adminNav.Contains("Rooms Management"), "Navigation respects authenticated roles");
    var catalog = await guest.GetStringAsync("/CustomerRooms");
    Check(catalog.Contains("TEST-A") && !catalog.Contains("TEST-OFF") && catalog.Contains("Breakfast") && catalog.Contains("Photo coming soon"),
        "Public catalog renders enabled rooms, services and placeholder");
    var filtered = await guest.GetStringAsync("/CustomerRooms?RoomType=Suite&MinimumPrice=90&MaximumPrice=110");
    Check(filtered.Contains("TEST-A") && filtered.Contains("TEST-GALLERY"), "Legacy type and price parameters do not filter available rooms");
    Check((await guest.GetStringAsync("/CustomerRooms?MinimumPrice=200&MaximumPrice=100")).Contains("TEST-A"), "Obsolete invalid filters do not suppress the catalog");
    Check(!catalog.Contains("stay-filter") && !catalog.Contains("name=\"CheckInDate\"") && !catalog.Contains("name=\"RoomType\""), "Catalog filter controls removed");
    Check(catalog.Contains($"href=\"/CustomerRooms/Details/{room.Id}\""), "View Details links to the room");
    var detailsHtml = await guest.GetStringAsync($"/CustomerRooms/Details/{gallery.Id}");
    Check(detailsHtml.Contains("name=\"Booking.CheckInDate\"") && detailsHtml.Contains("name=\"Booking.CheckOutDate\"") && detailsHtml.Contains("Calculate Price"), "Details retains booking date inputs and price preview");
    Check(detailsHtml.Contains("roomGallery") && detailsHtml.Contains("/test-photo-1.jpg") && detailsHtml.Contains("/test-photo-2.jpg"), "Public details render image carousel");
    Check((await guest.GetAsync("/CustomerRooms/Details/2147483647")).StatusCode == HttpStatusCode.NotFound, "Unknown room rejected");
    var admin = await adminClient.GetStringAsync("/Rooms");
    Check(admin.Contains("createRoomModal") && admin.Contains($"editRoomModal-{room.Id}") && admin.Contains("servicesRoomModal")
        && admin.Contains("imagesRoomModal") && admin.Contains("deleteRoomModal"), "Existing Admin Rooms modals still render");
    var home = await guest.GetStringAsync("/");
    Check(home.Contains("Welcome to Our Hotel") && home.Contains("Browse Rooms") && home.Contains("href=\"/CustomerRooms\"")
        && home.Contains("home-hero") && home.Contains("Contact Us") && home.Contains("href=\"/Contact\""),
        "Hotel landing page renders welcome hero and working Rooms/Contact links");
    var unauthorized = await Submit(guest, "/Bookings/Create", room.Id, start, start.AddDays(4));
    Check(unauthorized.StatusCode == HttpStatusCode.Redirect && unauthorized.Headers.Location!.ToString().Contains("/Account/Login"), "Anonymous booking creation challenged by Identity");
    Check((await guest.GetAsync("/Bookings/MyBookings")).StatusCode == HttpStatusCode.Redirect, "Anonymous My Bookings challenged");
    Check((await firstClient.PostAsync("/Bookings/Create", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest,
        "Authenticated POST without anti-forgery token rejected");
    var invalid = await Submit(firstClient, "/Bookings/Create", room.Id, start, start);
    Check(invalid.StatusCode == HttpStatusCode.OK && (await invalid.Content.ReadAsStringAsync()).Contains("Check-out must be after check-in"), "Invalid booking dates render server errors");
    var missing = await Submit(guest, "/CustomerRooms/Preview", room.Id, null, null);
    Check((await missing.Content.ReadAsStringAsync()).Contains("field is required"), "Required dates validated on preview");
    var past = await Submit(guest, "/CustomerRooms/Preview", room.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    Check((await past.Content.ReadAsStringAsync()).Contains("cannot be in the past"), "Past dates rejected on preview");
    var preview = await Submit(firstClient, "/CustomerRooms/Preview", room.Id, start, start.AddDays(4));
    Check((await preview.Content.ReadAsStringAsync()).Contains("560.00"), "Server-calculated preview renders expected price");
    var create = await Submit(firstClient, "/Bookings/Create", room.Id, start, start.AddDays(4), tamper: true);
    Check(create.StatusCode == HttpStatusCode.Redirect && create.Headers.Location!.ToString().Contains("MyBookings"), "Authenticated creation redirects to My Bookings");
    var saved = await database.Bookings.AsNoTracking().SingleAsync();
    Check(saved.UserId == firstId && saved.TotalAmount == 560m && saved.BookingStatus == BookingStatus.Pending && saved.RejectionReason == null,
        "Browser user/price/status tampering ignored; server saves Pending with authoritative amount");
    var own = await firstClient.GetStringAsync("/Bookings/MyBookings");
    Check(own.Contains("TEST-A") && own.Contains("Pending") && own.Contains("awaiting approval"), "Owner sees booking and confirmation");
    Check(!(await secondClient.GetStringAsync($"/Bookings/MyBookings?userId={firstId}&id={saved.Id}")).Contains("TEST-A"), "Other user cannot select owner's bookings via URL");
    var overlap = await Submit(secondClient, "/Bookings/Create", room.Id, start.AddDays(1), start.AddDays(3));
    Check((await overlap.Content.ReadAsStringAsync()).Contains("no longer available"), "Pending booking blocks overlapping creation");
    var datedCatalog = await guest.GetStringAsync($"/CustomerRooms?CheckInDate={start:yyyy-MM-dd}&CheckOutDate={start.AddDays(4):yyyy-MM-dd}");
    Check(datedCatalog.Contains("TEST-A") && datedCatalog.Contains("TEST-GALLERY"), "Catalog remains general availability despite date parameters and active bookings");
    var overlapPreview = await Submit(guest, "/CustomerRooms/Preview", room.Id, start, start.AddDays(4));
    Check((await overlapPreview.Content.ReadAsStringAsync()).Contains("not available for those dates"), "Details price preview still rejects overlapping dates");
    var adjacent = await Submit(secondClient, "/Bookings/Create", room.Id, start.AddDays(4), start.AddDays(5));
    Check(adjacent.StatusCode == HttpStatusCode.Redirect, "Adjacent stay on checkout date is allowed");

    foreach (var status in new[] { BookingStatus.Approved, BookingStatus.Rejected, BookingStatus.Completed })
    {
        await database.Bookings.Where(b => b.Id == saved.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.BookingStatus, status)
            .SetProperty(b => b.RejectionReason, status == BookingStatus.Rejected ? "Test reason" : null));
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CustomerBookingService>();
        Check(await service.HasOverlapAsync(room.Id, start, start.AddDays(4)) == (status == BookingStatus.Approved), $"{status} availability behavior");
        if (status == BookingStatus.Rejected)
            Check((await firstClient.GetStringAsync("/Bookings/MyBookings")).Contains("Test reason"), "Rejected booking reason rendered for owner");
    }
    var concurrentStart = start.AddDays(20);
    var concurrent = await Task.WhenAll(
        Submit(firstClient, "/Bookings/Create", gallery.Id, concurrentStart, concurrentStart.AddDays(2)),
        Submit(secondClient, "/Bookings/Create", gallery.Id, concurrentStart, concurrentStart.AddDays(2)));
    Check(concurrent.Count(r => r.StatusCode == HttpStatusCode.Redirect) == 1
        && await database.Bookings.CountAsync(b => b.RoomId == gallery.Id) == 1, "Concurrent overlapping requests save exactly one booking");
    var disabledRequest = await Submit(firstClient, "/Bookings/Create", disabled.Id, start, start.AddDays(1));
    Check((await disabledRequest.Content.ReadAsStringAsync()).Contains("no longer available"), "Disabled room cannot be booked");
    await ManagementChecks.RunAsync(database, app.Services, guest, firstClient, adminClient, firstId, room.Id, start, Check);
    Console.WriteLine($"All {count} hotel regression checks passed.");
}
finally
{
    if (app != null)
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
    if (!connection.InitialCatalog.StartsWith("HotelCustomerChecks_", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing to clean an unexpected database.");
    await database.Database.EnsureDeletedAsync();
    Console.WriteLine("Disposable test database removed.");
}
