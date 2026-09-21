using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Range(0, int.MaxValue)]
        public int CurrentQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }
    }
}