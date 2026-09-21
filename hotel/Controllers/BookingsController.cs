using System.ComponentModel.DataAnnotations;
using hotel.Data;
using hotel.Models;
using hotel.Services;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly CustomerBookingService _bookings;
        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _context;
        private readonly AdminBookingService _adminBookings;

        public BookingsController(CustomerBookingService bookings, UserManager<ApplicationUser> users,
            ApplicationDbContext context, AdminBookingService adminBookings)
        {
            _bookings = bookings;
            _users = users;
            _context = context;
            _adminBookings = adminBookings;
        }

        [Authorize(Roles = "Admin"), HttpGet]
        public async Task<IActionResult> Index(BookingStatus? status)
        {
            if (!ModelState.IsValid || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
            var query = _context.Bookings.AsNoTracking().Include(b => b.User).Include(b => b.Room).AsQueryable();
            if (status.HasValue) query = query.Where(b => b.BookingStatus == status);
            return View(new AdminBookingsViewModel { Status = status, Bookings = await query.OrderByDescending(b => b.Id).ToListAsync() });
        }

        [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Approve(int id) => ChangeStatus(id, BookingStatus.Approved);

        [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Reject(int id, string? rejectionReason) => ChangeStatus(id, BookingStatus.Rejected, rejectionReason);

        [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Complete(int id) => ChangeStatus(id, BookingStatus.Completed);

        private async Task<IActionResult> ChangeStatus(int id, BookingStatus target, string? reason = null)
        {
            if (!ModelState.IsValid) return BadRequest();
            try
            {
                var result = await _adminBookings.TransitionAsync(id, target, reason);
                if (!result.Found) return NotFound();
                TempData[result.Error == null ? "Success" : "Error"] = result.Error ?? $"Booking #{id} is now {target}.";
            }
            catch (SqlException error) when (error.Number == 1205)
            {
                TempData["Error"] = "The booking changed during this request. Refresh and try again.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "The booking could not be updated. Refresh and try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Customer"), HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();
            return View(await _bookings.GetMyBookingsAsync(user.Id));
        }

        [Authorize(Roles = "Customer"), HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Booking")] BookingRequestViewModel request)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Challenge();
            if (ModelState.IsValid)
            {
                try
                {
                    if (await _bookings.CreateAsync(request, user.Id))
                    {
                        TempData["BookingSuccess"] = "Your booking request has been submitted successfully and is awaiting approval.";
                        return RedirectToAction(nameof(MyBookings));
                    }
                    ModelState.AddModelError(string.Empty, "This room is no longer available for those dates. Please choose another stay.");
                }
                catch (ValidationException error)
                {
                    ModelState.AddModelError(string.Empty, error.Message);
                }
                catch (SqlException error) when (error.Number == 1205)
                {
                    ModelState.AddModelError(string.Empty, "Availability changed while booking. Please calculate the price again and retry.");
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Your booking could not be saved. Please check availability and try again.");
                }
            }

            var model = await _bookings.GetDetailsAsync(request.RoomId, request);
            return model == null ? NotFound() : View("~/Views/CustomerRooms/Details.cshtml", model);
        }
    }
}
