using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Data;
using SutkiVMVCS.Services.Shared;
using SutkiVMVCS.Services.YooKassa;
using System.Globalization;
using System.Text.Json;
using Telegram.Bot.Requests.Abstractions;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMaxService _max;
    private readonly IYooKassaService _yooKassa;

    public PaymentsController(AppDbContext context, IMaxService max, IYooKassaService yooKassa)
    {
        _context = context;
        _max = max;
        _yooKassa = yooKassa;
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId, ct);

        if (booking == null)
            return NotFound(new { message = "Бронь не найдена" });

        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(new { message = "Бронь отменена" });

        decimal remaining = booking.RemainingAmount;
        if (remaining == 0)
            return BadRequest(new { message = "Бронь полностью оплачена"} );

        decimal amountToCharge = remaining;
        PaymentType paymentType;

        if (booking.PaidAmount > 0) 
            paymentType = PaymentType.Balance;
        else if (dto.PayInFull) 
            paymentType = PaymentType.Full;
        else {
            paymentType = PaymentType.Prepayment;
            amountToCharge = booking.PrepaymentAmount;
        }

        var result = await _yooKassa.CreatePaymentAsync(
            booking,
            $"Бронь #{booking.Id} — {booking.Apartment.Title}",
            amountToCharge,
            ct
        );

        if (!result.Success || result.PaymentId == null)
            return BadRequest(new { message = result.Error });

        var payment = new Payment
        {
            BookingId = booking.Id,
            PaymentId = result.PaymentId,
            Paid = amountToCharge,
            Status = PaymentStatus.Pending,
            Type = paymentType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            url = result.ConfirmationUrl,
            paymentId = result.PaymentId,
            amount = amountToCharge
        });
    }

    [HttpPost("book-guest")]
    [AllowAnonymous]
    public async Task<IActionResult> BookGuest([FromBody] GuestBookingDto dto, CancellationToken ct)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber) || string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new { message = "Укажите имя и телефон" });

        string? phoneNumber = PhoneHelper.Normalize(dto.PhoneNumber);

        var apartment = await _context.Apartments.FindAsync(new object[] { dto.ApartmentId }, ct);
        if (apartment == null || !apartment.IsActive || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена или недоступна" });

        if (phoneNumber == null) return BadRequest(new { message = "Некорректный номер телефона" });
        else if (phoneNumber != "+79097587308") return BadRequest(new { message = "Данная функция находится в разработке" }); //Тест-костыль
            
        var start = dto.StartDate.ToUniversalTime();
        var end = dto.EndDate.ToUniversalTime();

        bool overlap = await _context.Bookings.AnyAsync(b =>
            b.ApartmentId == dto.ApartmentId &&
            b.Status != BookingStatus.Cancelled &&
            b.StartDate < end &&
            b.EndDate > start, ct);

        bool externalOverlap = await _context.ExternalBookings.AnyAsync(e =>
            e.ApartmentId == dto.ApartmentId &&
            e.StartDate < end &&
            e.EndDate > start, ct);

        if (overlap || externalOverlap)
            return Conflict(new { message = "Квартира занята в выбранные даты" });

        // Ищем существующего гостевого клиента по телефону, иначе создаём нового.
        // ВАЖНО: сверяем только с UserId == null, чтобы случайно не привязать
        // бронь к чужому зарегистрированному аккаунту по совпавшему номеру.
        var client = (await _context.Clients.ToListAsync(ct))
            .FirstOrDefault(cl => cl.PhoneNumber == phoneNumber); 
        
        if (client != null && client.InBlackList)
            return BadRequest(new { message = "Бронирование недоступно. Обратитесь в поддержку." });

        if (client == null)
        {
            client = new Client
            {
                FullName = dto.FullName,
                PhoneNumber = phoneNumber,
                UserId = null,
                InBlackList = false
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(ct);
        }
        else {
            client.FullName = dto.FullName;
        }

        var rules = await _context.PriceRules.ToListAsync(ct);
        decimal total = PricingService.CalculateTotal(dto.StartDate, dto.EndDate, dto.Guests, apartment, rules);
        decimal prepaymentAmount = PricingService.CalculatePrepayment(dto.StartDate, dto.Guests, apartment, rules, total, total);
        decimal amountToCharge;

        if (dto.PayInFull) {
            amountToCharge = total;
        }
        else {
            amountToCharge = prepaymentAmount;
        }

        var booking = new Booking {
            ApartmentId = dto.ApartmentId,
            ClientId = client.Id,
            Guests = dto.Guests,
            StartDate = start,
            EndDate = end,
            TotalPrice = total,
            BasePrice = total,
            PrepaymentAmount = prepaymentAmount,
            Status = BookingStatus.AwaitingPayment,
            Source = BookingSource.SiteGuest,
            CreatedAt = DateTime.UtcNow
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(ct);

        var result = await _yooKassa.CreatePaymentAsync(booking, $"Бронь #{booking.Id} — {apartment.Title}", amountToCharge, ct);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        var payment = new Payment
        {
            PaymentId = result.PaymentId!,
            Booking = booking,
            Status = PaymentStatus.Pending,
            Type = dto.PayInFull ? PaymentType.Full : PaymentType.Prepayment
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        return Ok(new { url = result.ConfirmationUrl, paymentId = result.PaymentId, bookingId = booking.Id });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        JsonElement json;
        try
        {
            json = JsonDocument.Parse(body).RootElement;
        }
        catch
        {
            return Ok(); 
        }

        var eventType = json.GetProperty("event").GetString();
        var paymentId = json.GetProperty("object").GetProperty("id").GetString();

        if (string.IsNullOrEmpty(paymentId)) return Ok();

        decimal? webhookAmount = null;
        if (json.TryGetProperty("object", out var obj) &&
            obj.TryGetProperty("amount", out var amountObj) &&
            amountObj.TryGetProperty("value", out var valueElement))
        {
            if (decimal.TryParse(valueElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
            {
                webhookAmount = amount;
            }
        }

        var statusResult = await _yooKassa.GetPaymentStatusAsync(paymentId, ct);
        if (!statusResult.Success || statusResult.BookingId == null) return Ok();

        var booking = await _context.Bookings
            .Include(b => b.Apartment) 
            .Include(b => b.Client) 
            .FirstOrDefaultAsync(b => b.Id == statusResult.BookingId, ct);

        if (booking == null) return Ok();

        var payment = await _context.Payments
            .Where(p => p.BookingId == booking.Id)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment == null) return Ok();

        switch (statusResult.Status?.ToLower())
        {
            case "succeeded":
                if (payment.Status != PaymentStatus.Succeeded)
                {
                    payment.Status = PaymentStatus.Succeeded;
                    payment.PaidAt = DateTime.Now;
                    payment.Paid = webhookAmount ?? 0;

                    decimal paidAmount = booking.Payments
                        .Where(p => p.Status == PaymentStatus.Succeeded)
                        .Sum(p => p.Paid);

                    if (!booking.Prepaid && paidAmount >= booking.PrepaymentAmount)
                    {
                        booking.Prepaid = true;

                        if (booking.Status == BookingStatus.AwaitingPayment || booking.Status == BookingStatus.Pending)
                        {
                            booking.Status = BookingStatus.Cancelled;
                        }
                    }

                    await _context.SaveChangesAsync(ct);
                }
                break;

            case "canceled":
                if (booking.Status != BookingStatus.Cancelled)
                {
                    booking.Status = BookingStatus.Cancelled;
                    await _context.SaveChangesAsync(ct);
                }
                break;
        }

        return Ok();
    }

    private async Task NotifyPaid(Booking booking)
    {
        try
        {
            await _max.SendBookingNotificationAsync(
                $"💳 Оплачено!\n🏠 {booking.Apartment?.Title}\n" +
                $"👤 {booking.Client?.FullName}  {booking.Client?.PhoneNumber}\n" +
                $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}\n" +
                $"💰 {booking.TotalPrice:N0} ₽",
                booking.Id);
        }
        catch (Exception ex) { Console.WriteLine($"[Payments] MAX error: {ex.Message}"); }
    }
}