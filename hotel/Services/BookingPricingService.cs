using hotel.Data;
using System.ComponentModel.DataAnnotations;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace hotel.Services
{
    public class BookingPricingService
    {
        private readonly ApplicationDbContext _context;

        public BookingPricingService(ApplicationDbContext context) => _context = context;

        public async Task<BookingPriceViewModel> CalculateAsync(decimal nightlyPrice, DateTime checkIn, DateTime checkOut)
        {
            var seasons = await _context.SeasonPricings.AsNoTracking()
                .Where(s => s.StartDate < checkOut && s.EndDate >= checkIn).ToListAsync();
            var nights = (checkOut.Date - checkIn.Date).Days;
            var offers = await _context.Offers.AsNoTracking()
                .Where(o => o.MinimumNights <= nights).ToListAsync();
            try
            {
                var price = Calculate(nightlyPrice, checkIn, checkOut, seasons, offers);
                if (price.TotalAmount > 9999999999999999.99m)
                    throw new ValidationException("The price for this stay exceeds the supported amount. Please contact the hotel.");
                return price;
            }
            catch (OverflowException)
            {
                throw new ValidationException("The price for this stay exceeds the supported amount. Please contact the hotel.");
            }
        }

        public static BookingPriceViewModel Calculate(decimal nightlyPrice, DateTime checkIn, DateTime checkOut,
            IEnumerable<SeasonPricing> seasons, IEnumerable<Offer> offers)
        {
            checkIn = checkIn.Date;
            checkOut = checkOut.Date;
            if (checkOut <= checkIn || nightlyPrice < 0)
                throw new ArgumentException("A valid stay and nightly price are required.");

            var nights = (checkOut - checkIn).Days;
            var applicableSeasons = seasons.Where(s => s.StartDate.Date < checkOut && s.EndDate.Date >= checkIn
                && s.StartDate <= s.EndDate && s.PriceMultiplier > 0).ToList();

            // Split at season boundaries, rather than querying or looping over every night.
            var boundaries = new SortedSet<DateTime> { checkIn, checkOut };
            foreach (var season in applicableSeasons)
            {
                if (season.StartDate.Date > checkIn) boundaries.Add(season.StartDate.Date);
                if (season.EndDate.Date < checkOut.AddDays(-1)) boundaries.Add(season.EndDate.Date.AddDays(1));
            }

            var points = boundaries.ToArray();
            decimal seasonalSubtotal = 0;
            for (var i = 0; i < points.Length - 1; i++)
            {
                // Season end dates are inclusive. Overlapping seasons use the highest multiplier.
                var multiplier = applicableSeasons
                    .Where(s => s.StartDate.Date <= points[i] && s.EndDate.Date >= points[i])
                    .Select(s => s.PriceMultiplier).DefaultIfEmpty(1m).Max();
                seasonalSubtotal += (points[i + 1] - points[i]).Days * nightlyPrice * multiplier;
            }

            var discount = offers.Where(o => o.MinimumNights > 0 && o.MinimumNights <= nights
                    && o.DiscountPercentage >= 0 && o.DiscountPercentage <= 100)
                .Select(o => o.DiscountPercentage).DefaultIfEmpty(0m).Max();
            var baseSubtotal = Round(nights * nightlyPrice);
            seasonalSubtotal = Round(seasonalSubtotal);
            var discountAmount = Round(seasonalSubtotal * discount / 100m);
            return new BookingPriceViewModel(nights, baseSubtotal, seasonalSubtotal - baseSubtotal,
                discount, discountAmount, seasonalSubtotal - discountAmount);
        }

        private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
