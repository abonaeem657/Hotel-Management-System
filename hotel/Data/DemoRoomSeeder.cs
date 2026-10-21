using hotel.Models;
using Microsoft.EntityFrameworkCore;

namespace hotel.Data;

/// <summary>Adds missing demo rooms to a development database without changing existing rooms.</summary>
public static class DemoRoomSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existingNumbers = await db.Rooms
            .Where(room => room.RoomNumber == "D101" || room.RoomNumber == "D204" || room.RoomNumber == "D310")
            .Select(room => room.RoomNumber).ToListAsync();
        if (existingNumbers.Count == 3) return;

        var existing = await db.RoomServices.ToListAsync();
        RoomService Service(string name)
        {
            var service = existing.FirstOrDefault(x => x.ServiceName == name);
            if (service != null) return service;
            service = new RoomService { ServiceName = name };
            existing.Add(service);
            return service;
        }

        var demoRooms = new[]
        {
            new Room
            {
                RoomNumber = "D101", RoomType = "Classic King", BasePricePerNight = 85m,
                Description = "A calm retreat with a king bed, soft natural light, a comfortable reading chair, and warm oak details. Ideal for a relaxed city stay.",
                IsAvailable = true, ImagePath = "/images/demo/classic-king.jpg",
                RoomImages = new List<RoomImage> { new() { ImagePath = "/images/demo/classic-king.jpg" } },
                RoomServices = new List<RoomService> { Service("Wi-Fi"), Service("Breakfast"), Service("Air conditioning") }
            },
            new Room
            {
                RoomNumber = "D204", RoomType = "Deluxe Twin", BasePricePerNight = 110m,
                Description = "Two generous beds, a bright window, a practical desk, and quiet sage accents make this room a welcoming choice for friends or colleagues.",
                IsAvailable = true, ImagePath = "/images/demo/deluxe-twin.jpg",
                RoomImages = new List<RoomImage> { new() { ImagePath = "/images/demo/deluxe-twin.jpg" } },
                RoomServices = new List<RoomService> { Service("Wi-Fi"), Service("Breakfast"), Service("Workspace") }
            },
            new Room
            {
                RoomNumber = "D310", RoomType = "Signature Suite", BasePricePerNight = 175m,
                Description = "An airy suite with a king bed, a separate seating corner, panoramic windows, and layered natural materials for an unhurried stay.",
                IsAvailable = true, ImagePath = "/images/demo/signature-suite.jpg",
                RoomImages = new List<RoomImage> { new() { ImagePath = "/images/demo/signature-suite.jpg" } },
                RoomServices = new List<RoomService> { Service("Wi-Fi"), Service("Breakfast"), Service("Seating area"), Service("Workspace") }
            }
        };
        db.Rooms.AddRange(demoRooms.Where(room => !existingNumbers.Contains(room.RoomNumber)));
        await db.SaveChangesAsync();
    }
}
