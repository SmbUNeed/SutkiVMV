using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// ═══════════════════════════════════════════════════════════════════════
//  ИНТЕРФЕЙС — только публичные методы которые нужны другим сервисам
// ═══════════════════════════════════════════════════════════════════════

public interface IMaxService
{
    Task SendNotificationAsync(string message);
    Task SendBookingNotificationAsync(string message, int bookingId);
}

// ═══════════════════════════════════════════════════════════════════════
//  БОТ
// ═══════════════════════════════════════════════════════════════════════

public class MaxBotWorker : BackgroundService, IMaxService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _http;
    private readonly long _adminChatId;
    private const string BaseUrl = "https://platform-api.max.ru";

    public MaxBotWorker(IConfiguration config, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _adminChatId = long.Parse(config["MaxBot:AdminChatId"]!);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(config["MaxBot:Token"]!);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LONG POLLING
    // ═══════════════════════════════════════════════════════════════════

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await _http.DeleteAsync($"{BaseUrl}/subscriptions", ct); } catch { }

        long marker = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = $"{BaseUrl}/updates?limit=50&timeout=30";
                if (marker > 0) url += $"&marker={marker}";

                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) { await Task.Delay(3000, ct); continue; }

                var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
                var updates = json?["updates"]?.AsArray();
                if (updates != null)
                    foreach (var upd in updates)
                        try { await HandleUpdate(upd!, ct); }
                        catch (Exception ex) { Console.WriteLine($"[MaxBot] {ex.Message}"); }

                var last = json?["marker"]?.GetValue<long>();
                if (last.HasValue) marker = last.Value;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex) { Console.WriteLine($"[MaxBot] Polling: {ex.Message}"); await Task.Delay(5000, ct); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  РОУТЕР АПДЕЙТОВ
    // ═══════════════════════════════════════════════════════════════════

    async Task HandleUpdate(JsonNode upd, CancellationToken ct)
    {
        switch (upd["update_type"]?.GetValue<string>())
        {
            case "message_created":
                var senderId = upd["message"]?["sender"]?["user_id"]?.GetValue<long>() ?? 0;
                var chatId = upd["message"]?["recipient"]?["chat_id"]?.GetValue<long>() ?? 0;
                var text = upd["message"]?["body"]?["text"]?.GetValue<string>() ?? "";
                if (senderId != _adminChatId) return;
                await OnMessage(chatId, text, ct);
                break;

            case "message_callback":
                var cbUserId = upd["callback"]?["user"]?["user_id"]?.GetValue<long>() ?? 0;
                var cbChatId = upd["callback"]?["message"]?["recipient"]?["chat_id"]?.GetValue<long>() ?? 0;
                var cbPayload = upd["callback"]?["payload"]?.GetValue<string>() ?? "";
                var cbCallbackId = upd["callback"]?["callback_id"]?.GetValue<string>() ?? "";
                if (cbUserId != _adminChatId) return;
                await OnCallback(cbChatId, cbPayload, cbCallbackId, ct);
                break;

            case "bot_started":
                var startChatId = upd["chat_id"]?.GetValue<long>() ?? 0;
                await Send(startChatId, "👋 Привет!", ct);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ОБРАБОТКА СООБЩЕНИЙ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnMessage(long chatId, string text, CancellationToken ct)
    {
        if (text == "/start")
        {
            await Send(chatId, "Привет!", ct);
            return;
        }
        // любая логика
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ОБРАБОТКА КНОПОК
    // ═══════════════════════════════════════════════════════════════════

    async Task OnCallback(long chatId, string payload, string callbackId, CancellationToken ct)
    {
        // Сначала всегда отвечаем на callback — иначе кнопка крутится
        await _http.PostAsync(
            $"{BaseUrl}/answers?callback_id={callbackId}",
            new StringContent("{}", Encoding.UTF8, "application/json"), ct);

        if (payload.StartsWith("confirm_") || payload.StartsWith("cancel_"))
        {
            var parts = payload.Split('_');
            var bookingId = int.Parse(parts[1]);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await db.Bookings.FindAsync(bookingId);
            if (booking == null) { await Send(chatId, "❌ Бронь не найдена.", ct); return; }

            booking.Status = parts[0] == "confirm" ? "Confirmed" : "Cancelled";
            await db.SaveChangesAsync(ct);

            await Send(chatId,
                booking.Status == "Confirmed"
                    ? $"✅ Бронь #{bookingId} подтверждена"
                    : $"❌ Бронь #{bookingId} отменена", ct);
            return;
        }

        else await Send(chatId, $"Неизвестный callback: {payload}", ct);
    }

    async Task Send(long chatId, string text, CancellationToken ct)
    {
        var payload = new
        {
            recipient = new { chat_id = chatId },
            body = new { text }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _http.PostAsync($"{BaseUrl}/messages", content, ct);
    }

    public async Task SendNotificationAsync(string message)
    {
        await Send(_adminChatId, message, CancellationToken.None);
    }

    public async Task SendBookingNotificationAsync(string message, int bookingId)
    {
        var buttons = new List<object[]>
        {
            new object[]
            {
                new { type = "callback", text = "✅ Подтвердить", payload = $"confirm_{bookingId}" },
                new { type = "callback", text = "❌ Отменить",    payload = $"cancel_{bookingId}"  }
            }
        };
        await SendWithButtons(_adminChatId, message, buttons, CancellationToken.None);
    }

    async Task SendWithButtons(long chatId, string text, List<object[]> buttons, CancellationToken ct)
    {
        var payload = new
        {
            text,
            attachments = new[]
            {
                new
                {
                    type    = "inline_keyboard",
                    payload = new { buttons }
                }
            }
        };

        var body = JsonSerializer.Serialize(payload);
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?chat_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);

        if (!resp.IsSuccessStatusCode)
            Console.WriteLine($"[MaxBot] SendWithButtons error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }
}