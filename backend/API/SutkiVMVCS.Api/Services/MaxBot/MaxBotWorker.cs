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

    // ── Состояния диалога ────────────────────────────────────────────
    private enum State { None /*, ВашиСостояния */ }

    private readonly ConcurrentDictionary<long, State> _states = new();
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, string>> _tmp = new();

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
                        catch (Exception ex) { Log($"HandleUpdate error: {ex.Message}"); }

                var lastMarker = json?["marker"]?.GetValue<long>();
                if (lastMarker.HasValue) marker = lastMarker.Value;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex) { Log($"Polling error: {ex.Message}"); await Task.Delay(5000, ct); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  РОУТЕР АПДЕЙТОВ
    // ═══════════════════════════════════════════════════════════════════

    async Task HandleUpdate(JsonNode upd, CancellationToken ct)
    {
        var type = upd["update_type"]?.GetValue<string>();

        switch (type)
        {
            case "message_created":
                {
                    var msg = upd["message"]; if (msg == null) return;
                    var senderId = msg["sender"]?["user_id"]?.GetValue<long>() ?? 0;
                    var chatId = msg["recipient"]?["chat_id"]?.GetValue<long>() ?? 0;
                    var text = msg["body"]?["text"]?.GetValue<string>() ?? "";
                    if (senderId != _adminChatId) return;
                    await OnMessage(chatId, text, ct);
                    break;
                }
            case "message_callback":
                {
                    var cb = upd["callback"]; if (cb == null) return;
                    var userId = cb["user"]?["user_id"]?.GetValue<long>() ?? 0;
                    var chatId = cb["message"]?["recipient"]?["chat_id"]?.GetValue<long>() ?? userId;
                    var payload = cb["payload"]?.GetValue<string>() ?? "";
                    if (userId != _adminChatId) return;
                    await OnCallback(chatId, payload, ct);
                    break;
                }
            case "bot_started":
                {
                    var chatId = upd["chat_id"]?.GetValue<long>() ?? 0;
                    if (upd["user"]?["user_id"]?.GetValue<long>() != _adminChatId) return;
                    await SendMenu(chatId, "👋 Добро пожаловать!", ct);
                    break;
                }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ОБРАБОТКА СООБЩЕНИЙ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnMessage(long chatId, string text, CancellationToken ct)
    {
        text = text.Trim();

        // /cancel — сбрасывает любой диалог
        if (text == "/cancel")
        {
            _states.TryRemove(chatId, out _);
            _tmp.TryRemove(chatId, out _);
            await SendMenu(chatId, "❌ Действие отменено.", ct);
            return;
        }

        // Команды
        if (text.StartsWith('/'))
        {
            _states.TryRemove(chatId, out _);
            _tmp.TryRemove(chatId, out _);
            await OnCommand(chatId, text.Split('@')[0].ToLower(), ct);
            return;
        }

        // Диалог по состоянию
        var state = _states.GetValueOrDefault(chatId, State.None);
        await OnState(chatId, text, state, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  КОМАНДЫ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnCommand(long chatId, string cmd, CancellationToken ct)
    {
        switch (cmd)
        {
            case "/start":
                await SendMenu(chatId, "👋 Добро пожаловать в панель управления!", ct);
                break;

            // ── ПРИМЕР: команда с переходом в состояние ──────────────
            // case "/booking_create":
            //     _states[chatId] = State.CreateBooking_WaitApartment;
            //     await Send(chatId, "Введите ID квартиры:", ct);
            //     break;

            default:
                await SendMenu(chatId, "❓ Неизвестная команда.", ct);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ДИАЛОГ ПО СОСТОЯНИЮ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnState(long chatId, string text, State state, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (state)
        {
            // ── ПРИМЕР: ждём ввод от пользователя ────────────────────
            // case State.CreateBooking_WaitApartment:
            //     if (!int.TryParse(text, out int aptId))
            //     {
            //         await Send(chatId, "⚠️ Введите число:", ct); return;
            //     }
            //     var apt = await db.Apartments.FindAsync(aptId);
            //     if (apt == null) { await Send(chatId, "❌ Не найдена.", ct); return; }
            //     _tmp.GetOrAdd(chatId, _ => new())["apartmentId"] = aptId.ToString();
            //     _states[chatId] = State.CreateBooking_WaitClient;
            //     await Send(chatId, $"✅ {apt.Title}\n\nТеперь введите ID клиента:", ct);
            //     break;

            default:
                await SendMenu(chatId, "🏠 Используйте команды меню.", ct);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CALLBACK КНОПКИ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnCallback(long chatId, string data, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── ПРИМЕР: confirm_123 / cancel_123 ─────────────────────────
        if (data.StartsWith("confirm_") || data.StartsWith("cancel_"))
        {
            var parts = data.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out int bId))
            {
                var booking = await db.Bookings.FindAsync(bId);
                if (booking == null) { await Send(chatId, "❌ Бронь не найдена.", ct); return; }

                booking.Status = parts[0] == "confirm" ? "Confirmed" : "Cancelled";
                await db.SaveChangesAsync(ct);
                await Send(chatId, $"Бронь #{bId}: {(booking.Status == "Confirmed" ? "✅ Подтверждена" : "❌ Отменена")}", ct);
            }
            return;
        }

        // Добавляйте свои обработчики по аналогии:
        // if (data.StartsWith("apt_")) { ... }
        // if (data.StartsWith("cli_")) { ... }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  РЕАЛИЗАЦИЯ IMaxService — вызывается из других контроллеров
    // ═══════════════════════════════════════════════════════════════════

    public async Task SendNotificationAsync(string message)
        => await Send(_adminChatId, message, CancellationToken.None);

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

    // ═══════════════════════════════════════════════════════════════════
    //  ОТПРАВКА — базовые методы
    // ═══════════════════════════════════════════════════════════════════

    // Простое текстовое сообщение
    public async Task Send(long chatId, string text, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { text });
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?chat_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Log($"Send error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    // Сообщение + меню команд в тексте
    public async Task SendMenu(long chatId, string text, CancellationToken ct)
    {
        var menu =
            "📅 /bookings_today  |  🗓 /bookings_month\n" +
            "➕ /booking_create  |  🗑 /booking_delete\n" +
            "👤 /client_new      |  🔍 /client_search\n" +
            "📋 /client_info     |  📌 /client_bookings\n" +
            "🏢 /apartment_edit  |  ❌ /cancel";
        await Send(chatId, $"{text}\n\n─────────────\n{menu}", ct);
    }

    // Сообщение с инлайн-кнопками
    // Пример вызова:
    //   var buttons = new List<object[]>
    //   {
    //       new object[] {
    //           new { type = "callback", text = "Да",  payload = "confirm_1" },
    //           new { type = "callback", text = "Нет", payload = "cancel_1"  }
    //       }
    //   };
    //   await SendWithButtons(chatId, "Подтвердить?", buttons, ct);
    public async Task SendWithButtons(long chatId, string text, List<object[]> buttons, CancellationToken ct)
    {
        var payload = new
        {
            text,
            attachments = new[]
            {
                new { type = "inline_keyboard", payload = new { buttons } }
            }
        };
        var body = JsonSerializer.Serialize(payload);
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?chat_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Log($"SendWithButtons error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    // Длинное сообщение — автоматически бьётся на части по 4000 символов
    public async Task SendLong(long chatId, string text, CancellationToken ct)
    {
        const int max = 4000;
        if (text.Length <= max) { await Send(chatId, text, ct); return; }

        var chunk = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (chunk.Length + line.Length + 1 > max)
            {
                await Send(chatId, chunk.ToString(), ct);
                chunk.Clear();
            }
            chunk.AppendLine(line);
        }
        if (chunk.Length > 0) await Send(chatId, chunk.ToString(), ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  УТИЛИТЫ
    // ═══════════════════════════════════════════════════════════════════

    // Парсинг даты ДД.ММ.ГГГГ
    protected static bool TryParseDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date);

    // Эмодзи статуса брони
    protected static string StatusEmoji(string status) => status switch
    {
        "Confirmed" => "✅",
        "Cancelled" => "❌",
        "Pending" => "⏳",
        _ => "❓"
    };

    static void Log(string msg) => Console.WriteLine($"[MaxBot] {msg}");
}