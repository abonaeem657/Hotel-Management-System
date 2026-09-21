using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class RoomService
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;
        public ICollection<Room> Rooms { get; set; }
    = new List<Room>();
    }
}