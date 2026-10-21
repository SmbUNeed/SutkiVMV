using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMVCS.Services.Shared
{
    public class BookingService
    {
        private readonly AppDbContext _context;
        public BookingService(AppDbContext context) { 
            _context = context;
        }

        public async Task<(Booking? Booking, string? Error)> CreateAsync(Apartment apartment, int clientId, int guests,
    DateTime start, DateTime end, string source, CancellationToken ct, string status = "Pending", decimal? price = null)
        {
            var s = start.ToUniversalTime();
            var e = end.ToUniversalTime();

            bool busy =
                await _context.Bookings.AnyAsync(b =>
                    b.ApartmentId == apartment.Id && b.Status != "Cancelled" &&
                    b.StartDate < e && b.EndDate > s, ct) ||
                await _context.ExternalBookings.AnyAsync(x =>
                    x.ApartmentId == apartment.Id && x.StartDate < e && x.EndDate > s, ct);

            if (busy) return (null, "Квартира занята в выбранные даты");

            var rules = await _context.PriceRules.ToListAsync(ct);

            Booking? before = null, after = null;
            if (price == null)
            {
                before = await _context.Bookings.FirstOrDefaultAsync(b =>
                    b.ApartmentId == apartment.Id && b.ClientId == clientId &&
                    b.Guests == guests && b.Status == status &&
                    b.EndDate == s, ct);

                after = await _context.Bookings.FirstOrDefaultAsync(b =>
                    b.ApartmentId == apartment.Id && b.ClientId == clientId &&
                    b.Guests == guests && b.Status == status &&
                    b.StartDate == e, ct);
            }

            if (before != null || after != null)
            {
                // Оставляем более раннюю бронь, вторую (если есть) удаляем
                var target = before ?? after!;
                var mergedStart = before?.StartDate ?? s;
                var mergedEnd = after?.EndDate ?? e;

                target.StartDate = mergedStart;
                target.EndDate = mergedEnd;
                target.TotalPrice = PricingService.CalculateTotal(mergedStart, mergedEnd, guests, apartment, rules);

                if (before != null && after != null)
                    _context.Bookings.Remove(after);

                await _context.SaveChangesAsync(ct);
                return (target, null);
            }

            var basePrice = PricingService.CalculateTotal(s, e, guests, apartment, rules);
            var total = price ?? basePrice;
            var discount = basePrice - total;

            if (price < 0 || price > basePrice) return (null, "Некорректная цена");

            var booking = new Booking
            {
                ApartmentId = apartment.Id,
                ClientId = clientId,
                Guests = guests,
                StartDate = s,
                EndDate = e,
                Status = status,   // было "Pending", параметр status игнорировался
                Source = source,
                CreatedAt = DateTime.UtcNow,
                BasePrice = basePrice,
                TotalPrice = total,
                DiscountAmount = discount,
                PrepaymentAmount = PricingService.CalculatePrepayment(s, guests, apartment, rules, basePrice, total),
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync(ct);
            return (booking, null);
        }
    }
}
