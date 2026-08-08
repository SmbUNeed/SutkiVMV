using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Services;
using SutkiVMVCS.Services.YooKassa;
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
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId, ct);

        if (booking == null)
            return NotFound(new { message = "Бронь не найдена" });

        if (booking.Status != "Pending" && booking.Status != "AwaitingPayment")
            return BadRequest(new { message = "Бронь уже оплачена или отменена" });

        var result = await _yooKassa.CreatePaymentAsync(
            booking, $"Бронь #{booking.Id} — {booking.Apartment.Title}", ct);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        booking.PaymentId = result.PaymentId;
        await _context.SaveChangesAsync(ct);

        return Ok(new { url = result.ConfirmationUrl, paymentId = result.PaymentId });
    }

    [HttpPost("book-guest")]
    [AllowAnonymous]
    public async Task<IActionResult> BookGuest([FromBody] GuestBookingDto dto, CancellationToken ct)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber) || string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new { message = "Укажите имя и телефон" });

        var apartment = await _context.Apartments.FindAsync(new object[] { dto.ApartmentId }, ct);
        if (apartment == null || !apartment.IsActive || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена или недоступна" });

        var start = dto.StartDate.ToUniversalTime();
        var end = dto.EndDate.ToUniversalTime();

        bool overlap = await _context.Bookings.AnyAsync(b =>
            b.ApartmentId == dto.ApartmentId &&
            b.Status != "Cancelled" &&
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
        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.PhoneNumber == dto.PhoneNumber && c.UserId == null, ct);

        if (client != null && client.InBlackList)
            return BadRequest(new { message = "Бронирование недоступно. Обратитесь в поддержку." });

        if (client == null)
        {
            client = new Client
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                UserId = null,
                InBlackList = false
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(ct); // чтобы получить client.Id
        }

        var rules = await _context.PriceRules.ToListAsync(ct);
        decimal total = PricingService.CalculateTotal(
            new UserCreateBookingDto
            {
                ApartmentId = dto.ApartmentId, 
                StartDate = dto.StartDate, 
                EndDate = dto.EndDate,
                Guests = dto.Guests,
            }, apartment, rules);

        var booking = new Booking {
            ApartmentId = dto.ApartmentId,
            ClientId = client.Id,
            Guests = dto.Guests,
            StartDate = start,
            EndDate = end,
            TotalPrice = total,
            Status = "AwaitingPayment", // отличается от "Pending" — админу нечего подтверждать, пока не оплачено
            Source = "SiteGuest",
            CreatedAt = DateTime.UtcNow
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(ct);

        var result = await _yooKassa.CreatePaymentAsync(booking, $"Бронь #{booking.Id} — {apartment.Title}", ct);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        booking.PaymentId = result.PaymentId;
        await _context.SaveChangesAsync(ct);

        return Ok(new { url = result.ConfirmationUrl, paymentId = result.PaymentId, bookingId = booking.Id });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        JsonElement json;
        try { json = JsonDocument.Parse(body).RootElement; }
        catch { return Ok(); }

        var paymentId = json.GetProperty("object").GetProperty("id").GetString();
        if (string.IsNullOrEmpty(paymentId)) return Ok();

        // Не доверяем содержимому вебхука — перепроверяем статус напрямую в ЮKassa
        var status = await _yooKassa.GetPaymentStatusAsync(paymentId, ct);
        if (!status.Success || status.BookingId == null) return Ok();

        var booking = await _context.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == status.BookingId, ct);

        if (booking == null) return Ok();
        if (booking.PaymentId != paymentId) return Ok();

        switch (status.Status)
        {
            case "succeeded" when booking.Status != "Confirmed":
                booking.Status = "Confirmed";
                booking.PaidAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                await NotifyPaid(booking);
                break;

            case "canceled" when booking.Status != "Cancelled":
                booking.Status = "Cancelled";
                await _context.SaveChangesAsync(ct);
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