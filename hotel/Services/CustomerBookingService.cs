using System.ComponentModel.DataAnnotations;
using System.Data;
using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace hotel.Services
{
    public class CustomerBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly BookingPricingService _pricing;

        public CustomerBookingService(ApplicationDbContext context, BookingPricingService pricing)
        {
            _context = context;
            _pricing = pricing;
        }

        public IQueryable<Booking> ActiveBookings() => _context.Bookings.Where(b =>
            b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Approved);

        public Task<bool> HasOverlapAsync(int roomId, DateTime checkIn, DateTime checkOut) =>
            ActiveBookings().AnyAsync(b => b.RoomId == roomId && b.CheckInDate < checkOut && b.CheckOutDate > checkIn);

        public async Task<RoomDetailsViewModel?> GetDetailsAsync(int roomId, BookingRequestViewModel request)
        {
            var room = await _context.Rooms.AsNoTracking()
                .Include(r => r.RoomImages).Include(r => r.RoomServices).AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == roomId);
            return room == null ? null : new RoomDetailsViewModel
            {
                Room = CustomerRoomViewModel.FromRoom(room), Booking = request
            };
        }

        public async Task<BookingPriceViewModel?> PreviewAsync(BookingRequestViewModel request)
        {
            Validate(request);
            var room = await _context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RoomId && r.IsAvailable);
            if (room == null || await HasOverlapAsync(request.RoomId, request.CheckInDate!.Value.Date, request.CheckOutDate!.Value.Date))
                return null;
            return await _pricing.CalculateAsync(room.BasePricePerNight, request.CheckInDate!.Value.Date, request.CheckOutDate!.Value.Date);
        }

        public async Task<bool> CreateAsync(BookingRequestViewModel request, string userId)
        {
            Validate(request);
            if (string.IsNullOrEmpty(userId) || !await _context.Users.AnyAsync(u => u.Id == userId))
                throw new ValidationException("A valid signed-in account is required.");

            // The room lock serializes submissions for the same room. Serializable also protects
            // the overlap query against booking inserts until the transaction commits.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var room = await _context.Rooms
                .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.RoomId}")
                .SingleOrDefaultAsync();
            var checkIn = request.CheckInDate!.Value.Date;
            var checkOut = request.CheckOutDate!.Value.Date;
            if (room == null || !room.IsAvailable || await HasOverlapAsync(request.RoomId, checkIn, checkOut))
                return false;

            var price = await _pricing.CalculateAsync(room.BasePricePerNight, checkIn, checkOut);
            _context.Bookings.Add(new Booking
            {
                RoomId = room.Id,
                UserId = userId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                TotalAmount = price.TotalAmount,
                BookingStatus = BookingStatus.Pending,
                RejectionReason = null
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public Task<List<MyBookingViewModel>> GetMyBookingsAsync(string userId) => _context.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Id)
            .Select(b => new MyBookingViewModel
            {
                RoomNumber = b.Room.RoomNumber,
                RoomType = b.Room.RoomType,
                ImagePath = b.Room.RoomImages.OrderBy(i => i.Id).Select(i => i.ImagePath).FirstOrDefault(),
                CheckInDate = b.CheckInDate,
                CheckOutDate = b.CheckOutDate,
                TotalAmount = b.TotalAmount,
                Status = b.BookingStatus,
                RejectionReason = b.RejectionReason
            }).ToListAsync();

        private static void Validate(BookingRequestViewModel request) =>
            Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
    }
}
