using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace hotel.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
