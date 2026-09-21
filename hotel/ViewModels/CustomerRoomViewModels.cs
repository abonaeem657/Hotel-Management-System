using System.ComponentModel.DataAnnotations;
using hotel.Models;

namespace hotel.ViewModels
{
    public class BookingDatesViewModel : IValidatableObject
    {
        [Required, DataType(DataType.Date), Display(Name = "Check-in")]
        public DateTime? CheckInDate { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Check-out")]
        public DateTime? CheckOutDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CheckInDate.HasValue && CheckInDate.Value.Date < DateTime.Today)
                yield return new ValidationResult("Check-in cannot be in the past.", new[] { nameof(CheckInDate) });
            if (CheckInDate.HasValue && CheckOutDate.HasValue && CheckOutDate.Value.Date <= CheckInDate.Value.Date)
                yield return new ValidationResult("Check-out must be after check-in.", new[] { nameof(CheckOutDate) });
        }
    }

    public class BookingRequestViewModel : BookingDatesViewModel
    {
        [Range(1, int.MaxValue)]
        public int RoomId { get; set; }
    }

    public class CustomerRoomViewModel
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public decimal BasePricePerNight { get; set; }
        public string? Description { get; set; }
        public string ShortDescription => string.IsNullOrWhiteSpace(Description)
            ? "A comfortable space for your next stay."
            : Description.Length > 140 ? Description[..137] + "..." : Description;
        public bool IsAvailable { get; set; }
        public List<string> Images { get; set; } = new();
        public List<string> Services { get; set; } = new();

        public static CustomerRoomViewModel FromRoom(Room room) => new()
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            RoomType = room.RoomType,
            BasePricePerNight = room.BasePricePerNight,
            Description = room.Description,
            IsAvailable = room.IsAvailable,
            Images = room.RoomImages.OrderBy(i => i.Id).Select(i => i.ImagePath).ToList(),
            Services = room.RoomServices.OrderBy(s => s.ServiceName).Select(s => s.ServiceName).ToList()
        };
    }

    public class RoomCatalogViewModel
    {
        public List<CustomerRoomViewModel> Rooms { get; set; } = new();
    }

    public class RoomDetailsViewModel
    {
        public CustomerRoomViewModel Room { get; set; } = new();
        public BookingRequestViewModel Booking { get; set; } = new();
        public BookingPriceViewModel? Price { get; set; }
    }

    public record BookingPriceViewModel(int Nights, decimal BaseSubtotal, decimal SeasonalAdjustment,
        decimal DiscountPercentage, decimal DiscountAmount, decimal TotalAmount);

    public class MyBookingViewModel
    {
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;
        public decimal TotalAmount { get; set; }
        public BookingStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public string StatusClass => Status switch
        {
            BookingStatus.Approved => "bg-success",
            BookingStatus.Rejected => "bg-danger",
            BookingStatus.Completed => "bg-secondary",
            _ => "bg-warning text-dark"
        };
    }
}
