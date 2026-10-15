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
    DateTime start, DateTime end, string source, CancellationToken ct, string status = "Pending")
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

            // Соседние брони: одна заканчивается ровно в начале новой, другая начинается ровно в конце новой
            var before = await _context.Bookings.FirstOrDefaultAsync(b =>
                b.ApartmentId == apartment.Id && b.ClientId == clientId &&
                b.Guests == guests && b.Status == status &&
                b.EndDate == s, ct);

            var after = await _context.Bookings.FirstOrDefaultAsync(b =>
                b.ApartmentId == apartment.Id && b.ClientId == clientId &&
                b.Guests == guests && b.Status == status &&
                b.StartDate == e, ct);

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

                await _context.SaveChangesAsync(ct); // одна транзакция: update + delete
                return (target, null);
            }

            var booking = new Booking
            {
                ApartmentId = apartment.Id,
                ClientId = clientId,
                Guests = guests,
                StartDate = s,
                EndDate = e,
                TotalPrice = PricingService.CalculateTotal(s, e, guests, apartment, rules),
                Status = status,   // было "Pending", параметр status игнорировался
                Source = source,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync(ct);
            return (booking, null);
        }
    }
}
