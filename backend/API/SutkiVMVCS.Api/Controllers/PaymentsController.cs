using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IMaxService _max;
    private readonly HttpClient _http;

    public PaymentsController(AppDbContext context, IConfiguration config, IMaxService max, HttpClient http)
    {
        _context = context;
        _config = config;
        _max = max;
        _http = http;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  POST /api/payments/create
    //  Создаёт платёж в ЮKassa и возвращает ссылку для редиректа
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Apartment)
            .FirstOrDefaultAsync(b => b.Id == dto.BookingId, ct);

        if (booking == null)
            return NotFound(new { message = "Бронь не найдена" });

        if (booking.Status != "Pending")
            return BadRequest(new { message = "Бронь уже оплачена или отменена" });

        var shopId = _config["YooKassa:ShopId"]!;
        var secretKey = _config["YooKassa:SecretKey"]!;
        var returnUrl = _config["AllowedOrigin"]!.Split(',')[0];

        var payload = new
        {
            amount = new
            {
                value = booking.TotalPrice.ToString("F2", CultureInfo.InvariantCulture),
                currency = "RUB"
            },
            confirmation = new
            {
                type = "redirect",
                return_url = $"{returnUrl}/cabinet.html?booking={booking.Id}"
            },
            capture = true,
            description = $"Бронь #{booking.Id} — {booking.Apartment.Title}",
            metadata = new { booking_id = booking.Id }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.yookassa.ru/v3/payments");
        request.Headers.Add("Idempotence-Key", Guid.NewGuid().ToString());
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shopId}:{secretKey}")));
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YooKassa] Error: {body}");
            return BadRequest(new { message = "Ошибка создания платежа" });
        }

        var json = JsonDocument.Parse(body).RootElement;

        var confirmationUrl = json
            .GetProperty("confirmation")
            .GetProperty("confirmation_url")
            .GetString();

        var paymentId = json.GetProperty("id").GetString();

        // Сохраняем paymentId в бронь
        booking.PaymentId = paymentId;
        await _context.SaveChangesAsync(ct);

        return Ok(new { url = confirmationUrl, paymentId });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  POST /api/payments/webhook
    //  ЮKassa вызывает этот метод когда платёж прошёл
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var body = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        JsonElement json;
        try { json = JsonDocument.Parse(body).RootElement; }
        catch { return Ok(); }

        var eventType = json.GetProperty("event").GetString();

        switch (eventType)
        {
            case "payment.succeeded":
                await HandlePaymentSucceeded(json, ct);
                break;

            case "payment.canceled":
                await HandlePaymentCanceled(json, ct);
                break;
        }

        return Ok();
    }

    // ── Платёж прошёл ─────────────────────────────────────────────────

    async Task HandlePaymentSucceeded(JsonElement json, CancellationToken ct)
    {
        var bookingId = json
            .GetProperty("object")
            .GetProperty("metadata")
            .GetProperty("booking_id")
            .GetInt32();

        var booking = await _context.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null) return;

        booking.Status = "Confirmed";
        booking.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        try
        {
            await _max.SendBookingNotificationAsync(
                $"💳 Оплачено!\n" +
                $"🏠 {booking.Apartment?.Title}\n" +
                $"👤 {booking.Client?.FullName}  {booking.Client?.PhoneNumber}\n" +
                $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}\n" +
                $"💰 {booking.TotalPrice:N0} ₽",
                bookingId);
        }
        catch (Exception ex) { Console.WriteLine($"[Payments] MAX error: {ex.Message}"); }
    }

    // ── Платёж отменён ────────────────────────────────────────────────

    async Task HandlePaymentCanceled(JsonElement json, CancellationToken ct)
    {
        var bookingId = json
            .GetProperty("object")
            .GetProperty("metadata")
            .GetProperty("booking_id")
            .GetInt32();

        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null) return;

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync(ct);
    }
}

public record CreatePaymentDto(int BookingId);
