using SutkiVMV.Models;
using SutkiVMVCS.Services.YooKassa;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class YooKassaService : IYooKassaService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public YooKassaService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;

        _http.BaseAddress = new Uri("https://api.yookassa.ru/v3/");
        var shopId = _config["YooKassa:ShopId"]!;
        var secretKey = _config["YooKassa:SecretKey"]!;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shopId}:{secretKey}")));
    }

    public async Task<YooKassaPaymentResult> CreatePaymentAsync(Booking booking, string description, decimal amountToCharge, string clientPhone, string? clientEmail, CancellationToken ct)
    {
        string? cleanPhone = null;
        if (!string.IsNullOrWhiteSpace(clientPhone))
        {
            var digits = new string(clientPhone.Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits.StartsWith("7"))
                cleanPhone = digits;
            else if (digits.Length == 10 && digits.StartsWith("9"))
                cleanPhone = "7" + digits;
        }

        var payload = new
        {
            amount = new
            {
                value = amountToCharge.ToString("F2", CultureInfo.InvariantCulture),
                currency = "RUB"
            },
            confirmation = new
            {
                type = "redirect",
                return_url = $"https://sutkivmv.ru/public-booking.html?id={booking.Id}&token={booking.PublicToken}"
            },
            capture = true,
            description,
            metadata = new { booking_id = booking.Id },
            receipt = new
            {
                customer = new
                {
                    phone = cleanPhone,

                    // email = $"guest{booking.Id}@sutkivmv.ru" 
                },
                items = new[]
                {
                    new
                    {
                        description,
                        quantity = "1.00",
                        amount = new { value = amountToCharge.ToString("F2", CultureInfo.InvariantCulture), currency = "RUB" },
                        vat_code = 1, // 1 = "Без НДС" (для УСН)
                        payment_mode = "full_payment",
                        payment_subject = "service"
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "payments");
        request.Headers.Add("Idempotence-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YooKassa] Create error: {body}");
            return new YooKassaPaymentResult(false, null, null, "Ошибка создания платежа");
        }

        var json = JsonDocument.Parse(body).RootElement;
        var url = json.GetProperty("confirmation").GetProperty("confirmation_url").GetString();
        var paymentId = json.GetProperty("id").GetString();

        return new YooKassaPaymentResult(true, url, paymentId, null);
    }

    public async Task<YooKassaPaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken ct)
    {
        var response = await _http.GetAsync($"payments/{paymentId}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YooKassa] Status check error: {body}");
            return new YooKassaPaymentStatus(false, null, null, "Не удалось проверить статус платежа");
        }

        var json = JsonDocument.Parse(body).RootElement;
        var status = json.GetProperty("status").GetString();

        int? bookingId = null;

        if (json.TryGetProperty("metadata", out var meta) &&
            meta.TryGetProperty("booking_id", out var bIdElement))
        {
            string? bIdString = bIdElement.GetString();

            if (int.TryParse(bIdString, out int parsedId))
            {
                bookingId = parsedId;
            }
            else
            {
                Console.WriteLine($"[YooKassa] Warning: Не удалось распарсить booking_id из строки: '{bIdString}'");
            }
        }

        return new YooKassaPaymentStatus(true, status, bookingId, null);
    }
}