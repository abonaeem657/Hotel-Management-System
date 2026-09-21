using hotel.Models;

namespace hotel.ViewModels
{
    public class RoomServicesViewModel
    {
        public int RoomId { get; set; }

        public string RoomNumber { get; set; } = string.Empty;

        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        public List<RoomService> AvailableServices { get; set; } = new List<RoomService>();
    }
}