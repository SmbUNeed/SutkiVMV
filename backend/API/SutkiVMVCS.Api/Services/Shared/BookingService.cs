using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Data;

namespace SutkiVMVCS.Services.Shared
{
    public class BookingService
    {
        private readonly AppDbContext _context;
        public BookingService(AppDbContext context) { 
            _context = context;
        }

        public async Task<(Booking? Booking, string? Error)> CreateAsync(
            Apartment apartment,
            int clientId,
            int guests,
            DateTime start,
            DateTime end,
            BookingSource source,
            CancellationToken ct,
            BookingStatus status = BookingStatus.Pending,
            decimal? price = null)
        {
            var s = start.ToUniversalTime();
            var e = end.ToUniversalTime();

            bool busy =
                await _context.Bookings.AnyAsync(b =>
                    b.ApartmentId == apartment.Id && b.Status != BookingStatus.Canceled &&
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
                var target = before ?? after!;
                var mergedStart = before?.StartDate ?? s;
                var mergedEnd = after?.EndDate ?? e;

                // ── МИГРАЦИЯ ПЛАТЕЖЕЙ ──
                // Если удаляем after, переносим все его платежи на target (before)
                if (before != null && after != null)
                {
                    // ЗАМЕНИ 'Payment' НА РЕАЛЬНОЕ ИМЯ ТВОЕЙ МОДЕЛИ ПЛАТЕЖЕЙ (например, Transaction или BookingPayment)
                    var paymentsToMove = await _context.Payments
                        .Where(p => p.BookingId == after.Id)
                        .ToListAsync(ct);

                    foreach (var payment in paymentsToMove)
                    {
                        payment.BookingId = target.Id;
                    }

                    _context.Bookings.Remove(after);
                }

                // Обновляем параметры целевой брони
                target.StartDate = mergedStart;
                target.EndDate = mergedEnd;
                target.TotalPrice = PricingService.CalculateTotal(mergedStart, mergedEnd, guests, apartment, rules);

                // Статус НЕ меняем, как и требовалось. 
                // Теперь у target будет 2 записи о платежах (если предоплаты были у обеих броней).

                await _context.SaveChangesAsync(ct);
                return (target, null);
            }

            var basePrice = PricingService.CalculateTotal(s, e, guests, apartment, rules);
            var total = price ?? basePrice;
            var discount = basePrice - total;

            if (total < 0 || total > basePrice)
                return (null, "Некорректная цена");

            var booking = new Booking
            {
                ApartmentId = apartment.Id,
                ClientId = clientId,
                Guests = guests,
                StartDate = s,
                EndDate = e,
                Status = status,
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
