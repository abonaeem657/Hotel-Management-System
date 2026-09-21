using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var counts = await context.Bookings.GroupBy(b => b.BookingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Status, g => g.Count);
        return View(new AdminDashboardViewModel
        {
            TotalRooms = await context.Rooms.CountAsync(),
            AvailableRooms = await context.Rooms.CountAsync(r => r.IsAvailable),
            TotalCustomers = await (from membership in context.UserRoles
                join role in context.Roles on membership.RoleId equals role.Id
                where role.NormalizedName == "CUSTOMER" select membership.UserId).Distinct().CountAsync(),
            TotalBookings = counts.Values.Sum(),
            PendingBookings = counts.GetValueOrDefault(BookingStatus.Pending),
            ApprovedBookings = counts.GetValueOrDefault(BookingStatus.Approved),
            CompletedBookings = counts.GetValueOrDefault(BookingStatus.Completed),
            RejectedBookings = counts.GetValueOrDefault(BookingStatus.Rejected),
            LowStockItems = await context.Inventories.CountAsync(i => i.CurrentQuantity <= i.ReorderLevel),
            UnreadMessages = await context.ContactMessages.CountAsync(m => !m.IsRead),
            // Only checked-out (Completed) bookings count as revenue; this is not payment tracking.
            CompletedRevenue = await context.Bookings.Where(b => b.BookingStatus == BookingStatus.Completed)
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0m
        });
    }

    [HttpGet]
    public IActionResult Dashboard() => RedirectToAction(nameof(Index));
}
