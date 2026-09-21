using System.ComponentModel.DataAnnotations;
using hotel.Models;

namespace hotel.ViewModels;

public class SeasonPricingInput : IValidatableObject
{
    [Required, StringLength(100), Display(Name = "Season name")]
    public string SeasonName { get; set; } = string.Empty;
    [Required, DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime? StartDate { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "End date")]
    public DateTime? EndDate { get; set; }
    [Required, Range(typeof(decimal), "0.01", "9999999999999999.99"), Display(Name = "Price multiplier")]
    public decimal? PriceMultiplier { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndDate?.Date < StartDate?.Date)
            yield return new ValidationResult("End date must be on or after start date.", new[] { nameof(EndDate) });
        // The existing database stores decimal(18,2); reject silent rounding.
        if (PriceMultiplier.HasValue && decimal.Round(PriceMultiplier.Value, 2) != PriceMultiplier.Value)
            yield return new ValidationResult("Use at most two decimal places.", new[] { nameof(PriceMultiplier) });
    }
}

public class OfferInput : IValidatableObject
{
    [Required, Range(1, int.MaxValue), Display(Name = "Minimum nights")]
    public int? MinimumNights { get; set; }
    [Required, Range(typeof(decimal), "0", "100"), Display(Name = "Discount percentage")]
    public decimal? DiscountPercentage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DiscountPercentage.HasValue && decimal.Round(DiscountPercentage.Value, 2) != DiscountPercentage.Value)
            yield return new ValidationResult("Use at most two decimal places.", new[] { nameof(DiscountPercentage) });
    }
}

public class InventoryInput
{
    [Required, StringLength(100), Display(Name = "Item name")]
    public string ItemName { get; set; } = string.Empty;
    [Required, Range(0, int.MaxValue), Display(Name = "Current quantity")]
    public int? CurrentQuantity { get; set; }
    [Required, Range(0, int.MaxValue), Display(Name = "Reorder level")]
    public int? ReorderLevel { get; set; }
}

public class QuantityInput
{
    [Required, Range(0, int.MaxValue), Display(Name = "Current quantity")]
    public int? CurrentQuantity { get; set; }
}

public class ContactInput
{
    [Required, StringLength(100), Display(Name = "Sender name")]
    public string SenderName { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Message")]
    public string MessageBody { get; set; } = string.Empty;
}

public class AdminBookingsViewModel
{
    public BookingStatus? Status { get; set; }
    public List<Booking> Bookings { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalRooms { get; set; }
    public int AvailableRooms { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int RejectedBookings { get; set; }
    public int LowStockItems { get; set; }
    public int UnreadMessages { get; set; }
    public decimal CompletedRevenue { get; set; }
}
