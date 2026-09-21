using System.ComponentModel.DataAnnotations;

namespace hotel.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(100), Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, Phone, StringLength(30), Display(Name = "Phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
