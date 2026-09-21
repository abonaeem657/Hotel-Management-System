using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class SeasonPricing
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SeasonName { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99")]
        public decimal PriceMultiplier { get; set; }
    }
}
