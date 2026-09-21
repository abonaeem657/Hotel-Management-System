using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class ContactMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string MessageBody { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime SentDate { get; set; } = DateTime.Now;
    }
}