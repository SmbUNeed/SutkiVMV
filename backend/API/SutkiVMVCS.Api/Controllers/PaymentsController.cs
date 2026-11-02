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
using System.Security.Claims;
using System.Text.Json;
using Telegram.Bot.Requests.Abstractions;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IYooKassaService _yooKassa;

    public PaymentsController(AppDbContext context, IServiceScopeFactory scopeFactory, IYooKassaService yooKassa)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _yooKassa = yooKassa;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllPayments(CancellationToken ct) {
        var payments = await _context.Payments.ToListAsync(ct);
        return Ok(payments);
    }

    [HttpGet("booking/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPaymentsByBooking(int id, CancellationToken ct) {
        var payments = await _context.Payments.Where(p => p.BookingId == id).ToListAsync();
        return Ok(payments);
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Payments)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId, ct);

        if (booking == null)
            return NotFound(new { message = "Бронь не найдена" });

        if (booking.Status == BookingStatus.Canceled)
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
            booking.Client.PhoneNumber,
            booking.Client.Email,
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
            CreatedAt = DateTime.UtcNow,
            Url = result.ConfirmationUrl
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
            
        var start = dto.StartDate.ToUniversalTime();
        var end = dto.EndDate.ToUniversalTime();

        bool overlap = await _context.Bookings.AnyAsync(b =>
            b.ApartmentId == dto.ApartmentId &&
            b.Status != BookingStatus.Canceled &&
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

        var result = await _yooKassa.CreatePaymentAsync(booking, $"Бронь #{booking.Id} — {apartment.Title}", amountToCharge, client.PhoneNumber, client.Email, ct);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        var payment = new Payment
        {
            PaymentId = result.PaymentId!,
            Booking = booking,
            Status = PaymentStatus.Pending,
            Type = dto.PayInFull ? PaymentType.Full : PaymentType.Prepayment,
            CreatedAt = DateTime.UtcNow,
            Paid = amountToCharge,
            Url = result.ConfirmationUrl
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        return Ok(new { url = result.ConfirmationUrl, paymentId = result.PaymentId, bookingId = booking.Id });
    }

    [HttpPost("book-and-pay")]
    [Authorize]
    public async Task<IActionResult> BookAndPay([FromBody] UserCreateBookingDto dto, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var client = await _context.Clients.FirstAsync(c => c.UserId == userId);
        if (client == null) return NotFound(new { message = "Клиент не найден" });

        var apartment = await _context.Apartments.FindAsync(new object[] { dto.ApartmentId }, ct);
        if (apartment == null) return NotFound(new { message = "Квартира не найдена" });

        var rules = await _context.PriceRules.ToListAsync(ct);
        decimal total = PricingService.CalculateTotal(dto.StartDate, dto.EndDate, dto.Guests, apartment, rules);
        decimal prepaymentAmount = PricingService.CalculatePrepayment(dto.StartDate, dto.Guests, apartment, rules, total, total);
        decimal amountToCharge = dto.PayInFull ? total : prepaymentAmount;

        var booking = new Booking
        {
            ApartmentId = dto.ApartmentId,
            ClientId = client.Id,
            Guests = dto.Guests,
            StartDate = dto.StartDate.ToUniversalTime(),
            EndDate = dto.EndDate.ToUniversalTime(),
            TotalPrice = total,
            BasePrice = total,
            PrepaymentAmount = prepaymentAmount,
            Status = BookingStatus.AwaitingPayment,
            Source = BookingSource.SiteUser,
            CreatedAt = DateTime.UtcNow
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(ct);

        var result = await _yooKassa.CreatePaymentAsync(booking, $"Бронь #{booking.Id} — {apartment.Title}", amountToCharge, client.PhoneNumber, client.Email, ct);
        if (!result.Success) return BadRequest(new { message = result.Error });

        _context.Payments.Add(new Payment
        {
            PaymentId = result.PaymentId!,
            BookingId = booking.Id,
            Status = PaymentStatus.Pending,
            Type = dto.PayInFull ? PaymentType.Full : PaymentType.Prepayment,
            Paid = amountToCharge,
            CreatedAt = DateTime.UtcNow,
            Url = result.ConfirmationUrl
        });
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

        if (!json.TryGetProperty("event", out var eventProp) ||
            !json.TryGetProperty("object", out var obj) ||
            !obj.TryGetProperty("id", out var idProp))
        {
            return Ok();
        }

        var eventType = eventProp.GetString();
        var paymentId = idProp.GetString();

        if (string.IsNullOrEmpty(paymentId)) return Ok();

        decimal? webhookAmount = null;
        if (obj.TryGetProperty("amount", out var amountObj) &&
            amountObj.TryGetProperty("value", out var valueElement))
        {
            if (decimal.TryParse(valueElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
            {
                webhookAmount = amount;
            }
        }

        var statusResult = await _yooKassa.GetPaymentStatusAsync(paymentId, ct);
        if (!statusResult.Success || statusResult.BookingId == null)
        {
            return Ok();
        }

        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Apartment)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Client)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Payments)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);

        if (payment?.Booking == null) return Ok();

        var booking = payment.Booking;

        switch (statusResult.Status?.ToLower())
        {
            case "succeeded":
                if (payment.Status != PaymentStatus.Succeeded)
                {
                    payment.Status = PaymentStatus.Succeeded;
                    payment.PaidAt = DateTime.UtcNow;

                    if (webhookAmount.HasValue && payment.Paid == 0)
                    {
                        payment.Paid = webhookAmount.Value;
                    }

                    decimal paidAmount = booking.Payments
                        .Where(p => p.Status == PaymentStatus.Succeeded)
                        .Sum(p => p.Paid);

                    if (!booking.Prepaid && booking.PrepaymentAmount > 0 && paidAmount >= booking.PrepaymentAmount)
                    {
                        booking.Prepaid = true;
                    }

                    if (booking.Status == BookingStatus.AwaitingPayment || paidAmount >= booking.TotalPrice)
                    {
                        booking.Status = BookingStatus.Confirmed;
                    }

                    await _context.SaveChangesAsync(ct);
                }
                await NotifyPaid(booking, payment.Type);
                break;

            case "canceled":
                if (payment.Status != PaymentStatus.Canceled)
                {
                    payment.Status = PaymentStatus.Canceled;

                    if (booking.Status == BookingStatus.AwaitingPayment &&
                        !booking.Payments.Any(p => p.Status == PaymentStatus.Succeeded))
                    {
                        booking.Status = BookingStatus.Canceled;
                    }

                    await _context.SaveChangesAsync(ct);
                }
                break;
        }

        return Ok();
    }

    private async Task NotifyPaid(Booking booking, PaymentType type)
    {
        string pay;
        switch(type) {
            case PaymentType.Prepayment:
                pay = "Предоплата";
                break;
            case PaymentType.Balance:
                pay = "Доплата";
                break;
            case PaymentType.Full:
                pay = "Полная оплата";
                break;
            case PaymentType.Refund:
                pay = "Возврат";
                break;
            default:
                pay = "Операция";
                break;
        }

        using var scope = _scopeFactory.CreateScope();
        try {
            var max = scope.ServiceProvider.GetRequiredService<IMaxService>();
            await max.SendBookingNotificationAsync(
                $"💳 {pay}\n🏠 {booking.Apartment?.Title}\n" +
                $"👤 {booking.Client?.FullName}  {booking.Client?.PhoneNumber}\n" +
                $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}\n" +
                $"💰 {booking.TotalPrice:N0} ₽",
                booking.Id);
        } catch (Exception ex) { Console.WriteLine($"[Payments] MAX error: {ex.Message}"); }

        try {
            var data = new Dictionary<string, string>(){
                { "bookingId", booking.Id.ToString() }
            };
            var tokens = await _context.DeviceTokens.Select(t => t.Token).ToListAsync();
            var fcm = scope.ServiceProvider.GetRequiredService<FcmService>();
            
            foreach(var token in tokens) {
                await fcm.SendAsync(token, $"{pay}", $"{booking.Apartment?.Title ?? ""}: ${booking.StartDate} - ${booking.EndDate}");
            }
        } catch (Exception ex) { Console.WriteLine($"[Payments] Fcm error: {ex.Message}"); }
    }
}