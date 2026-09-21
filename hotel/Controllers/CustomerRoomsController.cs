using hotel.Data;
using System.ComponentModel.DataAnnotations;
using hotel.Services;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers
{
    [AllowAnonymous]
    public class CustomerRoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CustomerBookingService _bookings;

        public CustomerRoomsController(ApplicationDbContext context, CustomerBookingService bookings)
        {
            _context = context;
            _bookings = bookings;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rooms = await _context.Rooms.AsNoTracking().Where(r => r.IsAvailable)
                .Include(r => r.RoomImages).Include(r => r.RoomServices)
                .AsSplitQuery().OrderBy(r => r.BasePricePerNight).ThenBy(r => r.Id).ToListAsync();
            return View(new RoomCatalogViewModel
            {
                Rooms = rooms.Select(CustomerRoomViewModel.FromRoom).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, DateTime? checkInDate, DateTime? checkOutDate)
        {
            var model = await _bookings.GetDetailsAsync(id, new BookingRequestViewModel
            {
                RoomId = id, CheckInDate = checkInDate, CheckOutDate = checkOutDate
            });
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview([Bind(Prefix = "Booking")] BookingRequestViewModel request)
        {
            var model = await _bookings.GetDetailsAsync(request.RoomId, request);
            if (model == null) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    model.Price = await _bookings.PreviewAsync(request);
                    if (model.Price == null)
                        ModelState.AddModelError(string.Empty, "This room is not available for those dates. Please choose another stay.");
                }
                catch (ValidationException error)
                {
                    ModelState.AddModelError(string.Empty, error.Message);
                }
            }
            return View("Details", model);
        }
    }
}
