using System.Data;
using hotel.Data;
using hotel.Models;
using Microsoft.EntityFrameworkCore;

namespace hotel.Services;

public record BookingTransitionResult(bool Found, string? Error = null);

public class AdminBookingService(ApplicationDbContext context)
{
    public async Task<BookingTransitionResult> TransitionAsync(int id, BookingStatus target, string? reason = null)
    {
        // Read only the room ID before acquiring locks. All lifecycle changes use the same
        // room-first locking order as customer creation, then reload the current status.
        var roomId = await context.Bookings.AsNoTracking().Where(b => b.Id == id)
            .Select(b => (int?)b.RoomId).SingleOrDefaultAsync();
        if (roomId == null) return new(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var room = await context.Rooms.FromSqlInterpolated(
            $"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roomId.Value}").SingleOrDefaultAsync();
        var booking = await context.Bookings.SingleOrDefaultAsync(b => b.Id == id);
        if (booking == null || room == null) return new(false);

        var valid = target switch
        {
            BookingStatus.Approved or BookingStatus.Rejected => booking.BookingStatus == BookingStatus.Pending,
            BookingStatus.Completed => booking.BookingStatus == BookingStatus.Approved,
            _ => false
        };
        if (!valid) return new(true, "This booking's status has changed or this transition is not allowed. Refresh and try again.");
        if (target == BookingStatus.Rejected && string.IsNullOrWhiteSpace(reason))
            return new(true, "A rejection reason is required.");
        if (target == BookingStatus.Approved)
        {
            if (!room.IsAvailable) return new(true, "This room is currently disabled for booking.");
            if (await context.Bookings.AnyAsync(b => b.Id != id && b.RoomId == room.Id
                && b.BookingStatus == BookingStatus.Approved
                && b.CheckInDate < booking.CheckOutDate && b.CheckOutDate > booking.CheckInDate))
                return new(true, "This room already has an approved booking that overlaps these dates.");
        }

        booking.BookingStatus = target;
        booking.RejectionReason = target == BookingStatus.Rejected ? reason!.Trim() : null;
        // IsAvailable is a general enable/disable switch, never a booking lifecycle flag.
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true);
    }
}
