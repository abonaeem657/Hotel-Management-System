using
    System.ComponentModel.DataAnnotations;
namespace hotel.Models
{
    public class Room
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string RoomType { get; set; } = string.Empty;
        [Required]
        [Range(0.01, 100000)]
        public decimal BasePricePerNight { get; set; }
        public string ? ImagePath { get; set; }
        public bool IsAvailable { get; set; } = true;
        [StringLength(1000)]
        public string? Description { get; set; }
       
        //ريلاشن بين ال روم و ال بوكنج
        public ICollection<Booking> Bookings { get; set; }
            = new List<Booking>();
        // ريلاشن بين ال روم و ال روم سيرفيس
        public ICollection<RoomService> RoomServices { get; set; }
            = new List<RoomService>();
        // ريلاشن بين ال روم و ال روم ايميج 
        public ICollection<RoomImage> RoomImages { get; set; }
            = new List<RoomImage>();

    }
}
