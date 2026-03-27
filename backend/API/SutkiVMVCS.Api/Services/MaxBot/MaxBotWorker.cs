using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public interface IMaxService
{
    Task SendNotificationAsync(string message);
    Task SendBookingNotificationAsync(string message, int bookingId);
}

public class MaxBotWorker : BackgroundService, IMaxService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _http;
    private readonly long _adminChatId;
    private readonly long _adminUserId;
    private const string BaseUrl = "https://platform-api.max.ru";

    private enum State
    {
        None,
        Booking_WaitId,
        Client_SearchName,
        Client_SearchPhone,
        Client_InfoQuery,
        Apartment_InfoQuery,
    }

    private readonly ConcurrentDictionary<long, State> _states = new();

    public MaxBotWorker(IConfiguration config, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _adminChatId = long.Parse(config["MaxBot:AdminChatId"]!);
        _adminUserId = long.Parse(config["MaxBot:AdminUserId"]!);

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

                var last = json?["marker"]?.GetValue<long>();
                if (last.HasValue) marker = last.Value;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex) { Log($"Polling error: {ex.Message}"); await Task.Delay(5000, ct); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  РОУТЕР
    // ═══════════════════════════════════════════════════════════════════

    async Task HandleUpdate(JsonNode upd, CancellationToken ct)
    {
        switch (upd["update_type"]?.GetValue<string>())
        {
            case "message_created":
                {
                    var senderId = upd["message"]?["sender"]?["user_id"]?.GetValue<long>() ?? 0;
                    var chatId = upd["message"]?["recipient"]?["chat_id"]?.GetValue<long>() ?? 0;
                    var text = upd["message"]?["body"]?["text"]?.GetValue<string>() ?? "";
                    if (senderId != _adminUserId) return;
                    await OnMessage(chatId, text, ct);
                    break;
                }
            case "message_callback":
                {
                    var cbUserId = upd["callback"]?["user"]?["user_id"]?.GetValue<long>() ?? 0;
                    var cbChatId = upd["callback"]?["message"]?["recipient"]?["chat_id"]?.GetValue<long>() ?? 0;
                    Log($"RAW CALLBACK: {upd.ToJsonString()}");  // ← добавь
                    Log($"cbUserId={cbUserId}, cbChatId={cbChatId}");
                    var cbPayload = upd["callback"]?["payload"]?.GetValue<string>() ?? "";
                    var cbCallbackId = upd["callback"]?["callback_id"]?.GetValue<string>() ?? "";
                    if (cbUserId != _adminUserId) return;
                    await OnCallback(cbChatId, cbPayload, cbCallbackId, upd, ct);
                    break;
                }
            case "bot_started":
                {
                    var chatId = upd["chat_id"]?.GetValue<long>() ?? 0;
                    if (upd["user"]?["user_id"]?.GetValue<long>() != _adminUserId) return;
                    await SendMainMenu(chatId, "👋 Добро пожаловать!", ct);
                    break;
                }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  СООБЩЕНИЯ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnMessage(long chatId, string text, CancellationToken ct)
    {
        text = text.Trim();

        if (text is "/start" or "menu" or "/menu")
        {
            _states.TryRemove(chatId, out _);
            await SendMainMenu(chatId, "🏠 Главное меню", ct);
            return;
        }

        if (text == "/cancel" || text == "отмена")
        {
            _states.TryRemove(chatId, out _);
            await SendMainMenu(chatId, "❌ Отменено.", ct);
            return;
        }

        var state = _states.GetValueOrDefault(chatId, State.None);
        await OnState(chatId, text, state, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  СОСТОЯНИЯ
    // ═══════════════════════════════════════════════════════════════════

    async Task OnState(long chatId, string text, State state, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (state)
        {
            case State.Booking_WaitId:
                if (!int.TryParse(text, out int bId)) { await Send(chatId, "⚠️ Введите числовой ID брони:", ct); return; }
                _states.TryRemove(chatId, out _);
                await ShowBookingMenu(chatId, db, bId, ct);
                break;

            case State.Client_SearchName:
                _states.TryRemove(chatId, out _);
                await SearchClientsByName(chatId, db, text, ct);
                break;

            case State.Client_SearchPhone:
                _states.TryRemove(chatId, out _);
                await SearchClientsByPhone(chatId, db, text, ct);
                break;

            case State.Client_InfoQuery:
                _states.TryRemove(chatId, out _);
                await ShowClientInfo(chatId, db, text, ct);
                break;

            case State.Apartment_InfoQuery:
                _states.TryRemove(chatId, out _);
                await ShowApartmentMenu(chatId, db, text, ct);
                break;

            default:
                await SendMainMenu(chatId, "Используйте меню ниже:", ct);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CALLBACK
    // ═══════════════════════════════════════════════════════════════════

    async Task OnCallback(long chatId, string payload, string callbackId, JsonNode upd, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (payload == "menu")
        {
            await EditToMainMenu(callbackId, "🏠 Главное меню", ct);
            return;
        }

        if (payload == "bookings")
        {
            await EditToBookingsMenu(callbackId, ct);
            return;
        }

        if (payload == "bookings_today")
        {
            await AnswerCallback(callbackId, ct);
            await ShowBookingsToday(chatId, db, ct);
            return;
        }

        if (payload == "bookings_month")
        {
            await AnswerCallback(callbackId, ct);
            await ShowBookingsMonth(chatId, db, ct);
            return;
        }

        if (payload == "booking_by_id")
        {
            await AnswerCallback(callbackId, ct);
            _states[chatId] = State.Booking_WaitId;
            await Send(chatId, "Введите ID брони (или /cancel):", ct);
            return;
        }

        if (payload.StartsWith("confirm_") || payload.StartsWith("cancel_"))
        {
            var parts = payload.Split('_');
            var bookingId = int.Parse(parts[1]);

            var booking = await db.Bookings
                .Include(b => b.Apartment).Include(b => b.Client)
                .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

            if (booking == null) { await AnswerCallback(callbackId, ct); await Send(chatId, "❌ Бронь не найдена.", ct); return; }

            booking.Status = parts[0] == "confirm" ? "Confirmed" : "Cancelled";
            await db.SaveChangesAsync(ct);

            var originalText = upd["callback"]?["message"]?["body"]?["text"]?.GetValue<string>() ?? "";

            if (booking.Status == "Confirmed")
            {
                var newMsg = new
                {
                    text = originalText + "\n\n✅ Подтверждено",
                    attachments = new[] { new { type = "inline_keyboard", payload = new { buttons = new List<object[]>
                    {
                        new object[] { new { type = "callback", text = "❌ Отменить бронь", payload = $"cancel_{bookingId}" } },
                        new object[] { new { type = "callback", text = "◀️ Меню",           payload = "menu"               } }
                    }}}}
                };
                await _http.PostAsync($"{BaseUrl}/answers?callback_id={callbackId}",
                    new StringContent(JsonSerializer.Serialize(new { message = newMsg }), Encoding.UTF8, "application/json"), ct);
            }
            else
            {
                var newMsg = new { text = originalText + "\n\n❌ Бронь отменена", attachments = Array.Empty<object>() };
                await _http.PostAsync($"{BaseUrl}/answers?callback_id={callbackId}",
                    new StringContent(JsonSerializer.Serialize(new { message = newMsg }), Encoding.UTF8, "application/json"), ct);
            }
            return;
        }

        if (payload == "clients")
        {
            await EditToClientsMenu(callbackId, ct);
            return;
        }

        if (payload == "client_search_name")
        {
            await AnswerCallback(callbackId, ct);
            _states[chatId] = State.Client_SearchName;
            await Send(chatId, "Введите имя клиента (или /cancel):", ct);
            return;
        }

        if (payload == "client_search_phone")
        {
            await AnswerCallback(callbackId, ct);
            _states[chatId] = State.Client_SearchPhone;
            await Send(chatId, "Введите номер телефона (или /cancel):", ct);
            return;
        }

        if (payload == "client_info")
        {
            await AnswerCallback(callbackId, ct);
            _states[chatId] = State.Client_InfoQuery;
            await Send(chatId, "Введите ID или имя клиента (или /cancel):", ct);
            return;
        }

        if (payload.StartsWith("client_bookings_"))
        {
            await AnswerCallback(callbackId, ct);
            var clientId = int.Parse(payload.Replace("client_bookings_", ""));
            await ShowClientBookings(chatId, db, clientId, ct);
            return;
        }

        if (payload == "apartments")
        {
            await EditToApartmentsMenu(callbackId, ct);
            return;
        }

        if (payload == "apartments_list")
        {
            await AnswerCallback(callbackId, ct);
            await ShowApartmentsList(chatId, db, ct);
            return;
        }

        if (payload == "apartment_info")
        {
            await AnswerCallback(callbackId, ct);
            _states[chatId] = State.Apartment_InfoQuery;
            await Send(chatId, "Введите ID или название квартиры (или /cancel):", ct);
            return;
        }

        if (payload.StartsWith("apt_bookings_"))
        {
            await AnswerCallback(callbackId, ct);
            var aptId = int.Parse(payload.Replace("apt_bookings_", ""));
            await ShowApartmentBookings(chatId, db, aptId, ct);
            return;
        }

        await AnswerCallback(callbackId, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  МЕНЮ
    // ═══════════════════════════════════════════════════════════════════

    async Task SendMainMenu(long chatId, string text, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "📅 Брони",    payload = "bookings"   },
                new { type = "callback", text = "👤 Клиенты",  payload = "clients"    },
                new { type = "callback", text = "🏢 Квартиры", payload = "apartments" }
            }
        };
        await SendWithButtons(chatId, text, buttons, ct);
    }

    async Task EditToMainMenu(string callbackId, string text, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "📅 Брони",    payload = "bookings"   },
                new { type = "callback", text = "👤 Клиенты",  payload = "clients"    },
                new { type = "callback", text = "🏢 Квартиры", payload = "apartments" }
            }
        };
        await EditMessage(callbackId, text, buttons, ct);
    }

    async Task EditToBookingsMenu(string callbackId, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "📅 Сегодня",  payload = "bookings_today" },
                new { type = "callback", text = "🗓 На месяц", payload = "bookings_month" }
            },
            new object[] { new { type = "callback", text = "🔍 По ID",   payload = "booking_by_id" } },
            new object[] { new { type = "callback", text = "◀️ Назад",   payload = "menu"          } }
        };
        await EditMessage(callbackId, "📅 Брони", buttons, ct);
    }

    async Task EditToClientsMenu(string callbackId, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "🔍 По имени",    payload = "client_search_name"  },
                new { type = "callback", text = "📞 По телефону", payload = "client_search_phone" }
            },
            new object[] { new { type = "callback", text = "📋 Инфо клиента", payload = "client_info" } },
            new object[] { new { type = "callback", text = "◀️ Назад",         payload = "menu"        } }
        };
        await EditMessage(callbackId, "👤 Клиенты", buttons, ct);
    }

    async Task EditToApartmentsMenu(string callbackId, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "📋 Все квартиры",  payload = "apartments_list" },
                new { type = "callback", text = "🔍 Меню квартиры", payload = "apartment_info"  }
            },
            new object[] { new { type = "callback", text = "◀️ Назад", payload = "menu" } }
        };
        await EditMessage(callbackId, "🏢 Квартиры", buttons, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  БРОНИ
    // ═══════════════════════════════════════════════════════════════════

    async Task ShowBookingsToday(long chatId, AppDbContext db, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var bookings = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .Where(b => b.StartDate < tomorrow && b.EndDate > today && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title)
            .AsNoTracking().ToListAsync(ct);

        if (!bookings.Any()) { await Send(chatId, "📅 На сегодня броней нет.", ct); return; }

        var sb = new StringBuilder($"📅 Брони на сегодня ({today:dd.MM.yyyy}) — {bookings.Count} шт.\n\n");
        foreach (var b in bookings) sb.AppendLine(FormatBooking(b));
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowBookingsMonth(long chatId, AppDbContext db, CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(30);

        var bookings = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .Where(b => b.StartDate < end && b.EndDate > start && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title).ThenBy(b => b.StartDate)
            .AsNoTracking().ToListAsync(ct);

        if (!bookings.Any()) { await Send(chatId, "🗓 На ближайший месяц броней нет.", ct); return; }

        var sb = new StringBuilder($"🗓 Брони на 30 дней ({start:dd.MM} — {end:dd.MM.yyyy}) — {bookings.Count} шт.\n\n");
        string? lastApt = null;
        foreach (var b in bookings)
        {
            if (b.Apartment?.Title != lastApt) { lastApt = b.Apartment?.Title; sb.AppendLine($"🏢 {lastApt}"); }
            sb.AppendLine(FormatBookingShort(b));
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowBookingMenu(long chatId, AppDbContext db, int bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null) { await Send(chatId, "❌ Бронь не найдена.", ct); return; }

        var nights = (booking.EndDate - booking.StartDate).Days;
        var text =
            $"🆔 Бронь #{booking.Id}  {StatusEmoji(booking.Status)}\n" +
            $"🏢 {booking.Apartment?.Title}\n" +
            $"👤 {booking.Client?.FullName}  {booking.Client?.PhoneNumber}\n" +
            $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}  🌙{nights}\n" +
            $"💰 {booking.TotalPrice:N0} ₽";

        var buttons = new List<object[]>();
        if (booking.Status != "Confirmed")
            buttons.Add(new object[] { new { type = "callback", text = "✅ Подтвердить", payload = $"confirm_{bookingId}" } });
        if (booking.Status != "Cancelled")
            buttons.Add(new object[] { new { type = "callback", text = "❌ Отменить", payload = $"cancel_{bookingId}" } });

        await SendWithButtons(chatId, text, buttons, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  КЛИЕНТЫ
    // ═══════════════════════════════════════════════════════════════════

    async Task SearchClientsByName(long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        var q = query.ToLower();
        var clients = await db.Clients
            .Where(c => c.FullName.ToLower().Contains(q))
            .OrderBy(c => c.FullName).Take(20)
            .Include(c => c.Bookings)
            .AsNoTracking().ToListAsync(ct);

        if (!clients.Any()) { await Send(chatId, $"🔍 По запросу «{query}» ничего не найдено.", ct); return; }

        var sb = new StringBuilder($"🔍 Найдено: {clients.Count}\n\n");
        foreach (var c in clients)
        {
            sb.AppendLine($"🆔 {c.Id} · {c.FullName}");
            sb.AppendLine($"   📞 {c.PhoneNumber}");
            sb.AppendLine($"   📌 Броней: {c.Bookings.Count}\n");
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task SearchClientsByPhone(long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        var digits = new string(query.Where(char.IsDigit).ToArray());
        if (digits.Length > 10) digits = digits[^10..];

        var all = await db.Clients.Include(c => c.Bookings).AsNoTracking().ToListAsync(ct);
        var clients = all.Where(c =>
        {
            var cd = new string(c.PhoneNumber.Where(char.IsDigit).ToArray());
            if (cd.Length > 10) cd = cd[^10..];
            return cd.EndsWith(digits);
        }).Take(20).ToList();

        if (!clients.Any()) { await Send(chatId, $"📞 По номеру «{query}» ничего не найдено.", ct); return; }

        var sb = new StringBuilder($"📞 Найдено: {clients.Count}\n\n");
        foreach (var c in clients)
        {
            sb.AppendLine($"🆔 {c.Id} · {c.FullName}");
            sb.AppendLine($"   📞 {c.PhoneNumber}");
            sb.AppendLine($"   📌 Броней: {c.Bookings.Count}\n");
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowClientInfo(long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        Client? client = null;
        if (int.TryParse(query, out int id))
            client = await db.Clients.Include(c => c.Bookings).ThenInclude(b => b.Apartment)
                .AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        else
            client = await db.Clients.Include(c => c.Bookings).ThenInclude(b => b.Apartment)
                .AsNoTracking().FirstOrDefaultAsync(c => c.FullName.ToLower().Contains(query.ToLower()), ct);

        if (client == null) { await Send(chatId, "❌ Клиент не найден.", ct); return; }

        var totalSpent = client.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice);
        var activeCount = client.Bookings.Count(b => b.Status != "Cancelled");

        var text =
            $"👤 Клиент #{client.Id}\n" +
            $"───────────────────\n" +
            $"ФИО: {client.FullName}\n" +
            $"📞 {client.PhoneNumber}\n" +
            $"✉️ {(string.IsNullOrEmpty(client.Email) ? "—" : client.Email)}\n\n" +
            $"📌 Всего броней: {client.Bookings.Count}\n" +
            $"✅ Активных: {activeCount}\n" +
            $"💰 Потрачено: {totalSpent:N0} ₽";

        var buttons = new List<object[]>
        {
            new object[] { new { type = "callback", text = "📌 Все брони клиента", payload = $"client_bookings_{client.Id}" } }
        };
        await SendWithButtons(chatId, text, buttons, ct);
    }

    async Task ShowClientBookings(long chatId, AppDbContext db, int clientId, CancellationToken ct)
    {
        var client = await db.Clients.Include(c => c.Bookings).ThenInclude(b => b.Apartment)
            .AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client == null) { await Send(chatId, "❌ Клиент не найден.", ct); return; }

        var bookings = client.Bookings.OrderByDescending(b => b.StartDate).ToList();
        if (!bookings.Any()) { await Send(chatId, $"📌 У клиента {client.FullName} нет броней.", ct); return; }

        var sb = new StringBuilder($"📌 Брони клиента {client.FullName} ({bookings.Count} шт.)\n\n");
        foreach (var b in bookings) sb.AppendLine(FormatBookingShort(b));
        await SendLong(chatId, sb.ToString(), ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  КВАРТИРЫ
    // ═══════════════════════════════════════════════════════════════════

    async Task ShowApartmentsList(long chatId, AppDbContext db, CancellationToken ct)
    {
        var apts = await db.Apartments.Where(a => !a.IsDeleted).OrderBy(a => a.Title)
            .AsNoTracking().ToListAsync(ct);

        if (!apts.Any()) { await Send(chatId, "❌ Квартир нет.", ct); return; }

        var sb = new StringBuilder("🏢 Все квартиры:\n\n");
        foreach (var a in apts)
            sb.AppendLine($"🆔 {a.Id} · {(a.IsActive ? "🟢" : "🔴")} {a.Title}\n   📍 {a.Address}\n");

        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowApartmentMenu(long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        Apartment? apt = null;
        if (int.TryParse(query, out int id))
            apt = await db.Apartments.Include(a => a.ApartmentAmenities)
                .AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        else
            apt = await db.Apartments.Include(a => a.ApartmentAmenities)
                .AsNoTracking().FirstOrDefaultAsync(a => a.Title.ToLower().Contains(query.ToLower()) && !a.IsDeleted, ct);

        if (apt == null) { await Send(chatId, "❌ Квартира не найдена.", ct); return; }

        var amenities = apt.ApartmentAmenities?.Any() == true
            ? string.Join(", ", apt.ApartmentAmenities.Select(a => a.Amenity.Name))
            : "—";

        var text =
            $"🏢 {apt.Title}  {(apt.IsActive ? "🟢" : "🔴")}\n" +
            $"───────────────────\n" +
            $"🆔 ID: {apt.Id}\n" +
            $"📍 {apt.Address}\n" +
            $"💰 {apt.PricePerNight:N0} ₽/ночь\n" +
            $"🛏 Комнат: {apt.RoomsCount}  👥 Гостей: {apt.Guests}  📐 {apt.Area} м²\n" +
            $"✨ Удобства: {amenities}\n\n" +
            $"📝 {apt.Description}";

        var buttons = new List<object[]>
        {
            new object[] { new { type = "callback", text = "📌 Брони квартиры", payload = $"apt_bookings_{apt.Id}" } }
        };
        await SendWithButtons(chatId, text.Length > 3900 ? text[..3900] + "..." : text, buttons, ct);
    }

    async Task ShowApartmentBookings(long chatId, AppDbContext db, int aptId, CancellationToken ct)
    {
        var apt = await db.Apartments.FindAsync(aptId);
        if (apt == null) { await Send(chatId, "❌ Квартира не найдена.", ct); return; }

        var bookings = await db.Bookings
            .Include(b => b.Client)
            .Where(b => b.ApartmentId == aptId && b.Status != "Cancelled")
            .OrderByDescending(b => b.StartDate)
            .AsNoTracking().ToListAsync(ct);

        if (!bookings.Any()) { await Send(chatId, $"📌 У квартиры «{apt.Title}» нет броней.", ct); return; }

        var sb = new StringBuilder($"📌 Брони «{apt.Title}» ({bookings.Count} шт.)\n\n");
        foreach (var b in bookings)
        {
            var nights = (b.EndDate - b.StartDate).Days;
            sb.AppendLine($"#{b.Id} {StatusEmoji(b.Status)} {b.StartDate:dd.MM} — {b.EndDate:dd.MM.yyyy} 🌙{nights}");
            sb.AppendLine($"   👤 {b.Client?.FullName}  📞 {b.Client?.PhoneNumber}");
            sb.AppendLine($"   💰 {b.TotalPrice:N0} ₽\n");
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  IMaxService
    // ═══════════════════════════════════════════════════════════════════

    public async Task SendNotificationAsync(string message)
        => await Send(_adminChatId, message, CancellationToken.None);

    public async Task SendBookingNotificationAsync(string message, int bookingId)
    {
        Log($"SendBookingNotificationAsync called, bookingId={bookingId}");
        var buttons = new List<object[]>
        {
            new object[] {
                new { type = "callback", text = "✅ Подтвердить", payload = $"confirm_{bookingId}" },
                new { type = "callback", text = "❌ Отменить",    payload = $"cancel_{bookingId}"  }
            }
        };
        await SendWithButtons(_adminChatId, message, buttons, CancellationToken.None);
        Log($"SendBookingNotificationAsync done");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ОТПРАВКА
    // ═══════════════════════════════════════════════════════════════════

    async Task Send(long chatId, string text, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { text });
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?chat_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Log($"Send error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    async Task SendWithButtons(long chatId, string text, List<object[]> buttons, CancellationToken ct)
    {
        var payload = new { text, attachments = new[] { new { type = "inline_keyboard", payload = new { buttons } } } };
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?chat_id={chatId}",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Log($"SendWithButtons error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    async Task EditMessage(string callbackId, string text, List<object[]> buttons, CancellationToken ct)
    {
        var msg = new { text, attachments = new[] { new { type = "inline_keyboard", payload = new { buttons } } } };
        var body = JsonSerializer.Serialize(new { message = msg });
        await _http.PostAsync($"{BaseUrl}/answers?callback_id={callbackId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    async Task SendLong(long chatId, string text, CancellationToken ct)
    {
        const int max = 4000;
        if (text.Length <= max) { await Send(chatId, text, ct); return; }
        var chunk = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (chunk.Length + line.Length + 1 > max) { await Send(chatId, chunk.ToString(), ct); chunk.Clear(); }
            chunk.AppendLine(line);
        }
        if (chunk.Length > 0) await Send(chatId, chunk.ToString(), ct);
    }

    async Task AnswerCallback(string callbackId, CancellationToken ct)
        => await _http.PostAsync($"{BaseUrl}/answers?callback_id={callbackId}",
            new StringContent("{}", Encoding.UTF8, "application/json"), ct);

    // ═══════════════════════════════════════════════════════════════════
    //  УТИЛИТЫ
    // ═══════════════════════════════════════════════════════════════════

    string FormatBooking(Booking b)
    {
        var nights = (b.EndDate - b.StartDate).Days;
        return $"🆔 #{b.Id}  {StatusEmoji(b.Status)}\n" +
               $"  🏢 {b.Apartment?.Title ?? "—"}\n" +
               $"  👤 {b.Client?.FullName ?? "—"}  {b.Client?.PhoneNumber ?? ""}\n" +
               $"  📅 {b.StartDate:dd.MM.yyyy} — {b.EndDate:dd.MM.yyyy}  🌙{nights}\n" +
               $"  💰 {b.TotalPrice:N0} ₽\n";
    }

    string FormatBookingShort(Booking b)
    {
        var nights = (b.EndDate - b.StartDate).Days;
        return $"#{b.Id} {StatusEmoji(b.Status)} {b.StartDate:dd.MM} — {b.EndDate:dd.MM.yyyy} 🌙{nights} 💰{b.TotalPrice:N0}₽";
    }

    static string StatusEmoji(string status) => status switch
    {
        "Confirmed" => "✅",
        "Cancelled" => "❌",
        "Pending" => "⏳",
        _ => "❓"
    };

    static void Log(string msg) => Console.WriteLine($"[MaxBot] {msg}");
}