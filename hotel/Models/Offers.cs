using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class Offer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int MinimumNights { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }
    }
}
