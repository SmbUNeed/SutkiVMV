using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public class MaxBotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly long _adminId;
    private readonly string _token;

    private const string BaseUrl = "https://platform-api.max.ru";

    public MaxBotWorker(IConfiguration config, IServiceProvider serviceProvider)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _token = config["MaxBot:Token"]!;
        _adminId = long.Parse(config["MaxBot:AdminChatId"]!);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(_token);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── Состояния диалога ────────────────────────────────────────────────

    private enum BotState
    {
        None,
        CreateBooking_WaitApartment,
        CreateBooking_WaitClient,
        CreateBooking_WaitStartDate,
        CreateBooking_WaitEndDate,
        DeleteBooking_WaitId,
        NewClient_WaitName,
        NewClient_WaitPhone,
        NewClient_WaitEmail,
        ClientSearch_WaitQuery,
        ClientInfo_WaitId,
        ClientBookings_WaitId,
        EditApartment_WaitId,
        EditApartment_WaitField,
        EditApartment_WaitValue,
    }

    private readonly ConcurrentDictionary<long, BotState> _states = new();
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, string>> _tempData = new();

    // ── Long Polling ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Удаляем старый вебхук если есть, чтобы работал long polling
        try { await _http.DeleteAsync($"{BaseUrl}/subscriptions", stoppingToken); } catch { }

        long marker = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = $"{BaseUrl}/updates?limit=50&timeout=30";
                if (marker > 0) url += $"&marker={marker}";

                var resp = await _http.GetAsync(url, stoppingToken);
                if (!resp.IsSuccessStatusCode)
                {
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }

                var body = await resp.Content.ReadAsStringAsync(stoppingToken);
                var json = JsonNode.Parse(body);

                var updates = json?["updates"]?.AsArray();
                if (updates == null || updates.Count == 0) continue;

                foreach (var upd in updates)
                {
                    try { await HandleUpdate(upd!, stoppingToken); }
                    catch (Exception ex) { Console.WriteLine($"[MaxBot] Ошибка: {ex.Message}"); }
                }

                // Двигаем маркер к последнему событию
                var lastMarker = json?["marker"]?.GetValue<long>();
                if (lastMarker.HasValue) marker = lastMarker.Value;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaxBot] Polling error: {ex.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ГЛАВНЫЙ ОБРАБОТЧИК
    // ═══════════════════════════════════════════════════════════════════════

    async Task HandleUpdate(JsonNode upd, CancellationToken ct)
    {
        Console.WriteLine($"[MaxBot] UPDATE: {upd.ToJsonString()}");

        var type = upd["update_type"]?.GetValue<string>();

        switch (type)
        {
            case "message_created":
                var msg = upd["message"];
                if (msg == null) return;

                var senderId = msg["sender"]?["user_id"]?.GetValue<long>() ?? 0;
                var chatId = msg["recipient"]?["chat_id"]?.GetValue<long>()
                            ?? msg["sender"]?["user_id"]?.GetValue<long>() ?? 0;
                var text = msg["body"]?["text"]?.GetValue<string>() ?? "";

                if (senderId != _adminId) return;
                await HandleMessage(chatId, senderId, text, ct);
                break;

            case "message_callback":
                var cb = upd["callback"];
                if (cb == null) return;
                var cbUserId = cb["user"]?["user_id"]?.GetValue<long>() ?? 0;
                var cbChatId = cb["message"]?["recipient"]?["chat_id"]?.GetValue<long>()
                            ?? cbUserId;
                var cbData = cb["payload"]?.GetValue<string>() ?? "";
                var cbMsgId = cb["message"]?["body"]?["mid"]?.GetValue<string>()
                            ?? upd["message_id"]?.GetValue<string>() ?? "";

                if (cbUserId != _adminId) return;
                await HandleCallback(cbChatId, cbData, cbMsgId, ct);
                break;

            case "bot_started":
                var startChatId = upd["chat_id"]?.GetValue<long>() ?? 0;
                if (upd["user"]?["user_id"]?.GetValue<long>() != _adminId) return;
                await SendMenu(startChatId, "👋 Добро пожаловать в панель управления!", ct);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ОБРАБОТКА СООБЩЕНИЙ
    // ═══════════════════════════════════════════════════════════════════════

    async Task HandleMessage(long chatId, long userId, string text, CancellationToken ct)
    {
        text = text.Trim();
        var state = _states.GetValueOrDefault(chatId, BotState.None);

        if (text == "/cancel")
        {
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            await SendMenu(chatId, "❌ Действие отменено.", ct);
            return;
        }

        if (text.StartsWith('/'))
        {
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            await HandleCommand(chatId, text, ct);
            return;
        }

        await HandleByState(chatId, text, state, ct);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  КОМАНДЫ
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleCommand(long chatId, string cmd, CancellationToken ct)
    {
        cmd = cmd.Split('@')[0].ToLower();

        switch (cmd)
        {
            case "/start":
                await SendMenu(chatId, "👋 Добро пожаловать в панель управления!", ct);
                break;

            case "/bookings_today":
                await ShowBookingsToday(chatId, ct);
                break;

            case "/bookings_month":
                await ShowBookingsMonth(chatId, ct);
                break;

            case "/booking_create":
                _states[chatId] = BotState.CreateBooking_WaitApartment;
                await ShowApartmentsList(chatId, ct);
                break;

            case "/booking_delete":
                _states[chatId] = BotState.DeleteBooking_WaitId;
                await Send(chatId,
                    "🗑 Удаление брони\n\nВведите ID брони для удаления (отмена: /cancel):", ct);
                break;

            case "/client_new":
                _states[chatId] = BotState.NewClient_WaitName;
                _tempData[chatId] = new ConcurrentDictionary<string, string>();
                await Send(chatId, "👤 Новый клиент\n\nВведите ФИО клиента (отмена: /cancel):", ct);
                break;

            case "/client_search":
                _states[chatId] = BotState.ClientSearch_WaitQuery;
                await Send(chatId, "🔍 Поиск клиента\n\nВведите имя, телефон или email (отмена: /cancel):", ct);
                break;

            case "/client_info":
                _states[chatId] = BotState.ClientInfo_WaitId;
                await Send(chatId, "📋 Данные клиента\n\nВведите ID клиента (отмена: /cancel):", ct);
                break;

            case "/client_bookings":
                _states[chatId] = BotState.ClientBookings_WaitId;
                await Send(chatId, "📌 Брони клиента\n\nВведите ID клиента (отмена: /cancel):", ct);
                break;

            case "/apartment_edit":
                _states[chatId] = BotState.EditApartment_WaitId;
                await ShowApartmentsList(chatId, ct);
                break;

            default:
                await SendMenu(chatId, "❓ Неизвестная команда.", ct);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ОБРАБОТКА ПО СОСТОЯНИЮ
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleByState(long chatId, string text, BotState state, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (state)
        {
            // ── СОЗДАНИЕ БРОНИ ───────────────────────────────────────────

            case BotState.CreateBooking_WaitApartment:
                if (!int.TryParse(text, out int aptId))
                {
                    await Send(chatId, "⚠️ Введите корректный ID квартиры:", ct); return;
                }
                var apt = await db.Apartments.FindAsync(aptId);
                if (apt == null || apt.IsDeleted)
                {
                    await Send(chatId, "❌ Квартира не найдена. Попробуйте ещё раз:", ct); return;
                }
                _tempData[chatId] = new ConcurrentDictionary<string, string>
                { ["apartmentId"] = aptId.ToString(), ["apartmentTitle"] = apt.Title };
                _states[chatId] = BotState.CreateBooking_WaitClient;
                await ShowClientsList(chatId, db, ct);
                break;

            case BotState.CreateBooking_WaitClient:
                if (!int.TryParse(text, out int clientId))
                {
                    await Send(chatId, "⚠️ Введите корректный ID клиента:", ct); return;
                }
                var client = await db.Clients.FindAsync(clientId);
                if (client == null)
                {
                    await Send(chatId, "❌ Клиент не найден. Попробуйте ещё раз:", ct); return;
                }
                _tempData[chatId]["clientId"] = clientId.ToString();
                _tempData[chatId]["clientName"] = client.FullName;
                _states[chatId] = BotState.CreateBooking_WaitStartDate;
                await Send(chatId, $"✅ Клиент: {client.FullName}\n\n📅 Введите дату заезда (ДД.ММ.ГГГГ):", ct);
                break;

            case BotState.CreateBooking_WaitStartDate:
                if (!TryParseDate(text, out DateTime start))
                {
                    await Send(chatId, "⚠️ Неверный формат. Введите дату ДД.ММ.ГГГГ:", ct); return;
                }
                _tempData[chatId]["startDate"] = start.ToString("O");
                _states[chatId] = BotState.CreateBooking_WaitEndDate;
                await Send(chatId, $"✅ Дата заезда: {start:dd.MM.yyyy}\n\n📅 Введите дату выезда (ДД.ММ.ГГГГ):", ct);
                break;

            case BotState.CreateBooking_WaitEndDate:
                if (!TryParseDate(text, out DateTime end))
                {
                    await Send(chatId, "⚠️ Неверный формат. Введите дату ДД.ММ.ГГГГ:", ct); return;
                }
                var startDate = DateTime.Parse(_tempData[chatId]["startDate"]);
                if (end <= startDate)
                {
                    await Send(chatId, "⚠️ Дата выезда должна быть позже даты заезда:", ct); return;
                }
                await CreateBooking(chatId, db, startDate, end, ct);
                break;

            // ── УДАЛЕНИЕ БРОНИ ───────────────────────────────────────────

            case BotState.DeleteBooking_WaitId:
                if (!int.TryParse(text, out int bookingId))
                {
                    await Send(chatId, "⚠️ Введите корректный ID брони:", ct); return;
                }
                await DeleteBookingConfirm(chatId, db, bookingId, ct);
                break;

            // ── НОВЫЙ КЛИЕНТ ─────────────────────────────────────────────

            case BotState.NewClient_WaitName:
                if (text.Length < 2) { await Send(chatId, "⚠️ Имя слишком короткое. Введите ФИО:", ct); return; }
                _tempData[chatId]["name"] = text;
                _states[chatId] = BotState.NewClient_WaitPhone;
                await Send(chatId, $"✅ ФИО: {text}\n\n📞 Введите номер телефона:", ct);
                break;

            case BotState.NewClient_WaitPhone:
                _tempData[chatId]["phone"] = text;
                _states[chatId] = BotState.NewClient_WaitEmail;
                await Send(chatId, $"✅ Телефон: {text}\n\n✉️ Введите email (или прочерк -):", ct);
                break;

            case BotState.NewClient_WaitEmail:
                _tempData[chatId]["email"] = text == "-" ? "" : text;
                await CreateClient(chatId, db, ct);
                break;

            // ── ПОИСК / ИНФО / БРОНИ КЛИЕНТА ────────────────────────────

            case BotState.ClientSearch_WaitQuery:
                await SearchClients(chatId, db, text, ct);
                break;

            case BotState.ClientInfo_WaitId:
                if (!int.TryParse(text, out int cId)) { await Send(chatId, "⚠️ Введите корректный ID:", ct); return; }
                await ShowClientInfo(chatId, db, cId, ct);
                break;

            case BotState.ClientBookings_WaitId:
                if (!int.TryParse(text, out int cbId)) { await Send(chatId, "⚠️ Введите корректный ID:", ct); return; }
                await ShowClientBookings(chatId, db, cbId, ct);
                break;

            // ── РЕДАКТИРОВАНИЕ КВАРТИРЫ ──────────────────────────────────

            case BotState.EditApartment_WaitId:
                if (!int.TryParse(text, out int eAptId)) { await Send(chatId, "⚠️ Введите корректный ID:", ct); return; }
                var eApt = await db.Apartments.FindAsync(eAptId);
                if (eApt == null || eApt.IsDeleted) { await Send(chatId, "❌ Квартира не найдена.", ct); return; }
                _tempData[chatId] = new ConcurrentDictionary<string, string> { ["apartmentId"] = eAptId.ToString() };
                _states[chatId] = BotState.EditApartment_WaitField;
                await ShowEditApartmentMenu(chatId, eApt, ct);
                break;

            case BotState.EditApartment_WaitField:
                var validFields = new[] { "title", "address", "description", "price", "rooms", "guests", "area", "active" };
                if (!validFields.Contains(text.ToLower())) { await Send(chatId, "⚠️ Выберите поле из списка:", ct); return; }
                _tempData[chatId]["field"] = text.ToLower();
                _states[chatId] = BotState.EditApartment_WaitValue;
                await Send(chatId, GetFieldHint(text.ToLower()), ct);
                break;

            case BotState.EditApartment_WaitValue:
                await EditApartmentField(chatId, db, text, ct);
                break;

            default:
                await SendMenu(chatId, "🏠 Используйте команды меню.", ct);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ОБРАБОТКА CALLBACK КНОПОК
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleCallback(long chatId, string data, string messageId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Подтвердить / отменить бронь
        if (data.StartsWith("confirm_") || data.StartsWith("cancel_"))
        {
            var parts = data.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out int bId))
            {
                var booking = await db.Bookings
                    .Include(b => b.Apartment)
                    .Include(b => b.Client)
                    .FirstOrDefaultAsync(b => b.Id == bId, ct);

                if (booking == null) { await Send(chatId, "❌ Бронь не найдена!", ct); return; }

                booking.Status = parts[0] == "confirm" ? "Confirmed" : "Cancelled";
                var statusText = booking.Status == "Confirmed" ? "✅ Подтверждена" : "❌ Отменена";
                await db.SaveChangesAsync(ct);
                await Send(chatId, $"Статус брони #{bId} изменён: {statusText}", ct);
            }
            return;
        }

        // Выбор поля квартиры
        if (data.StartsWith("editfield_"))
        {
            var field = data.Replace("editfield_", "");
            if (_tempData.TryGetValue(chatId, out var td)) td["field"] = field;
            _states[chatId] = BotState.EditApartment_WaitValue;
            await Send(chatId, GetFieldHint(field), ct);
            return;
        }

        // Выбор квартиры
        if (data.StartsWith("apt_"))
        {
            if (int.TryParse(data.Replace("apt_", ""), out int aId))
            {
                var aptObj = await db.Apartments.FindAsync(aId, ct);
                if (aptObj == null) return;

                var curState = _states.GetValueOrDefault(chatId, BotState.None);
                if (!_tempData.ContainsKey(chatId)) _tempData[chatId] = new();
                _tempData[chatId]["apartmentId"] = aId.ToString();
                _tempData[chatId]["apartmentTitle"] = aptObj.Title;

                if (curState == BotState.EditApartment_WaitId)
                {
                    _states[chatId] = BotState.EditApartment_WaitField;
                    await ShowEditApartmentMenu(chatId, aptObj, ct);
                }
                else
                {
                    _states[chatId] = BotState.CreateBooking_WaitClient;
                    await ShowClientsList(chatId, db, ct);
                }
            }
            return;
        }

        // Выбор клиента
        if (data.StartsWith("cli_"))
        {
            if (int.TryParse(data.Replace("cli_", ""), out int cId))
            {
                var clientObj = await db.Clients.FindAsync(cId, ct);
                if (clientObj == null) return;
                _tempData[chatId]["clientId"] = cId.ToString();
                _tempData[chatId]["clientName"] = clientObj.FullName;
                _states[chatId] = BotState.CreateBooking_WaitStartDate;
                await Send(chatId, $"✅ Клиент: {clientObj.FullName}\n\n📅 Введите дату заезда (ДД.ММ.ГГГГ):", ct);
            }
            return;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  МЕТОДЫ-ДЕЙСТВИЯ
    // ═══════════════════════════════════════════════════════════════════════

    async Task ShowBookingsToday(long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var bookings = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .Where(b => b.StartDate < tomorrow && b.EndDate > today && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title)
            .AsNoTracking().ToListAsync(ct);

        if (!bookings.Any())
        {
            await SendMenu(chatId, "📅 На сегодня нет активных броней.", ct); return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📅 Брони на сегодня ({today:dd.MM.yyyy}) — {bookings.Count} шт.\n");
        foreach (var b in bookings) sb.AppendLine(FormatBooking(b));
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowBookingsMonth(long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(30);

        var bookings = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .Where(b => b.StartDate < end && b.EndDate > start && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title).ThenBy(b => b.StartDate)
            .AsNoTracking().ToListAsync(ct);

        if (!bookings.Any())
        {
            await SendMenu(chatId, "🗓 На ближайший месяц броней нет.", ct); return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🗓 Брони на 30 дней ({start:dd.MM} — {end:dd.MM.yyyy}) — {bookings.Count} шт.\n");

        string? lastApt = null;
        foreach (var b in bookings)
        {
            if (b.Apartment?.Title != lastApt)
            {
                lastApt = b.Apartment?.Title;
                sb.AppendLine($"\n🏢 {lastApt}");
            }
            sb.AppendLine(FormatBookingShort(b));
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowApartmentsList(long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var apts = await db.Apartments.Where(a => !a.IsDeleted).OrderBy(a => a.Title)
            .AsNoTracking().ToListAsync(ct);

        if (!apts.Any()) { await Send(chatId, "❌ Нет доступных квартир.", ct); return; }

        var state = _states.GetValueOrDefault(chatId, BotState.None);
        var header = state == BotState.EditApartment_WaitId
            ? "🏢 Выберите квартиру для редактирования:\n"
            : "🏢 Выберите квартиру для брони:\n";

        var sb = new StringBuilder(header + "\n");
        foreach (var a in apts)
            sb.AppendLine($"  {a.Id} · {(a.IsActive ? "🟢" : "🔴")} {a.Title} — {a.PricePerNight:N0} ₽/ночь");
        sb.AppendLine("\nВведите ID квартиры:");

        var buttons = apts.Take(8)
            .Select(a => new object[] { new { type = "callback", text = $"{(a.IsActive ? "🟢" : "🔴")} {a.Title}", payload = $"apt_{a.Id}" } })
            .ToList();

        await SendWithButtons(chatId, sb.ToString(), buttons, ct);
    }

    async Task ShowClientsList(long chatId, AppDbContext db, CancellationToken ct)
    {
        var clients = await db.Clients.OrderBy(c => c.FullName).Take(30)
            .AsNoTracking().ToListAsync(ct);

        var aptTitle = _tempData[chatId].GetValueOrDefault("apartmentTitle", "");
        var sb = new StringBuilder($"👤 Выберите клиента для «{aptTitle}»:\n\n");

        foreach (var c in clients)
            sb.AppendLine($"  {c.Id} · {c.FullName}  {c.PhoneNumber}");
        sb.AppendLine("\nВведите ID клиента:");

        var buttons = clients.Take(8)
            .Select(c => new object[] { new { type = "callback", text = c.FullName, payload = $"cli_{c.Id}" } })
            .ToList();

        await SendWithButtons(chatId, sb.ToString(), buttons, ct);
    }

    async Task CreateBooking(long chatId, AppDbContext db, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var d = _tempData[chatId];
        int apartmentId = int.Parse(d["apartmentId"]);
        int clientId = int.Parse(d["clientId"]);

        var apartment = await db.Apartments.FindAsync(apartmentId);
        var client = await db.Clients.FindAsync(clientId);

        if (apartment == null || client == null)
        {
            await Send(chatId, "❌ Ошибка: квартира или клиент не найдены.", ct);
            _states.TryRemove(chatId, out _); _tempData.TryRemove(chatId, out _);
            return;
        }

        bool overlap = await db.Bookings.AnyAsync(b =>
            b.ApartmentId == apartmentId && b.Status != "Cancelled" &&
            b.StartDate < endDate.ToUniversalTime() &&
            b.EndDate > startDate.ToUniversalTime(), ct);

        if (overlap)
        {
            await Send(chatId, "⚠️ Квартира занята в выбранные даты.\n\nВведите другую дату заезда (ДД.ММ.ГГГГ):", ct);
            _states[chatId] = BotState.CreateBooking_WaitStartDate;
            return;
        }

        var nights = (endDate - startDate).Days;
        var booking = new Booking
        {
            ApartmentId = apartmentId,
            ClientId = clientId,
            StartDate = startDate.ToUniversalTime(),
            EndDate = endDate.ToUniversalTime(),
            TotalPrice = apartment.PricePerNight * nights,
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        _states.TryRemove(chatId, out _); _tempData.TryRemove(chatId, out _);

        await SendMenu(chatId,
            $"✅ Бронь создана!\n\n" +
            $"🏢 Квартира: {apartment.Title}\n" +
            $"👤 Клиент: {client.FullName}  {client.PhoneNumber}\n" +
            $"📅 Заезд: {startDate:dd.MM.yyyy}\n" +
            $"📅 Выезд: {endDate:dd.MM.yyyy}\n" +
            $"🌙 Ночей: {nights}\n" +
            $"💰 Итого: {booking.TotalPrice:N0} ₽\n" +
            $"🆔 Бронь #{booking.Id}", ct);
    }

    async Task DeleteBookingConfirm(long chatId, AppDbContext db, int bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Apartment).Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            await Send(chatId, "❌ Бронь не найдена. Введите другой ID или /cancel:", ct); return;
        }

        _states.TryRemove(chatId, out _); _tempData.TryRemove(chatId, out _);

        var buttons = new List<object[]>
        {
            new object[]
            {
                new { type = "callback", text = "✅ Подтвердить отмену", payload = $"cancel_{bookingId}" },
                new { type = "callback", text = "↩️ Назад",              payload = "menu_back"           }
            }
        };

        await SendWithButtons(chatId,
            $"🗑 Удаление брони #{bookingId}\n\n" +
            $"🏢 {booking.Apartment?.Title}\n" +
            $"👤 {booking.Client?.FullName}\n" +
            $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}\n" +
            $"💰 {booking.TotalPrice:N0} ₽  |  Статус: {StatusEmoji(booking.Status)}\n\n" +
            $"Подтвердите отмену брони:",
            buttons, ct);
    }

    async Task CreateClient(long chatId, AppDbContext db, CancellationToken ct)
    {
        var d = _tempData[chatId];
        var client = new Client
        {
            FullName = d["name"],
            PhoneNumber = d["phone"],
            Email = d.GetValueOrDefault("email", "")
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        _states.TryRemove(chatId, out _); _tempData.TryRemove(chatId, out _);

        await SendMenu(chatId,
            $"✅ Клиент создан!\n\n" +
            $"🆔 ID: {client.Id}\n" +
            $"👤 ФИО: {client.FullName}\n" +
            $"📞 Тел: {client.PhoneNumber}\n" +
            $"✉️ Email: {(string.IsNullOrEmpty(client.Email) ? "—" : client.Email)}", ct);
    }

    async Task SearchClients(long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        var q = query.Trim().ToLower();
        var clients = await db.Clients
            .Where(c => c.FullName.ToLower().Contains(q)
                     || c.PhoneNumber.Contains(q)
                     || c.Email.ToLower().Contains(q))
            .OrderBy(c => c.FullName).Take(20)
            .Include(c => c.Bookings)
            .AsNoTracking().ToListAsync(ct);

        _states.TryRemove(chatId, out _);

        if (!clients.Any())
        {
            await SendMenu(chatId, $"🔍 По запросу «{query}» ничего не найдено.", ct); return;
        }

        var sb = new StringBuilder($"🔍 Найдено клиентов: {clients.Count}\n\n");
        foreach (var c in clients)
        {
            sb.AppendLine($"{c.Id} · {c.FullName}");
            sb.AppendLine($"    📞 {c.PhoneNumber}  {(c.Email != "" ? $"✉️ {c.Email}" : "")}");
            sb.AppendLine($"    📌 Броней: {c.Bookings.Count}\n");
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowClientInfo(long chatId, AppDbContext db, int clientId, CancellationToken ct)
    {
        var client = await db.Clients.Include(c => c.Bookings).ThenInclude(b => b.Apartment)
            .AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);

        _states.TryRemove(chatId, out _);
        if (client == null) { await SendMenu(chatId, "❌ Клиент не найден.", ct); return; }

        var totalSpent = client.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice);
        var activeCount = client.Bookings.Count(b => b.Status != "Cancelled");

        await SendMenu(chatId,
            $"👤 Клиент #{client.Id}\n" +
            $"───────────────────────\n" +
            $"ФИО: {client.FullName}\n" +
            $"Тел: {client.PhoneNumber}\n" +
            $"Email: {(string.IsNullOrEmpty(client.Email) ? "—" : client.Email)}\n\n" +
            $"📌 Всего броней: {client.Bookings.Count}\n" +
            $"✅ Активных: {activeCount}\n" +
            $"💰 Потрачено: {totalSpent:N0} ₽", ct);
    }

    async Task ShowClientBookings(long chatId, AppDbContext db, int clientId, CancellationToken ct)
    {
        var client = await db.Clients.Include(c => c.Bookings).ThenInclude(b => b.Apartment)
            .AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);

        _states.TryRemove(chatId, out _);
        if (client == null) { await SendMenu(chatId, "❌ Клиент не найден.", ct); return; }

        var bookings = client.Bookings.OrderBy(b => b.Apartment?.Title).ThenByDescending(b => b.StartDate).ToList();
        if (!bookings.Any()) { await SendMenu(chatId, $"📌 У клиента {client.FullName} нет броней.", ct); return; }

        var sb = new StringBuilder($"📌 Брони клиента: {client.FullName} ({bookings.Count} шт.)\n");
        string? lastApt = null;
        foreach (var b in bookings)
        {
            if (b.Apartment?.Title != lastApt)
            {
                lastApt = b.Apartment?.Title;
                sb.AppendLine($"\n🏢 {lastApt}");
            }
            sb.AppendLine(FormatBookingShort(b));
        }
        await SendLong(chatId, sb.ToString(), ct);
    }

    async Task ShowEditApartmentMenu(long chatId, Apartment apt, CancellationToken ct)
    {
        var buttons = new List<object[]>
        {
            new object[] { new { type = "callback", text = "📝 Название", payload = "editfield_title" },
                           new { type = "callback", text = "📍 Адрес",    payload = "editfield_address" } },
            new object[] { new { type = "callback", text = "💰 Цена",    payload = "editfield_price" },
                           new { type = "callback", text = "🛏 Комнаты", payload = "editfield_rooms" } },
            new object[] { new { type = "callback", text = "👥 Гости",   payload = "editfield_guests" },
                           new { type = "callback", text = "📐 Площадь", payload = "editfield_area" } },
            new object[] { new { type = "callback", text = "🔤 Описание", payload = "editfield_description" } },
            new object[] { new { type = "callback",
                text    = apt.IsActive ? "🔴 Скрыть" : "🟢 Активировать",
                payload = "editfield_active" } },
        };

        await SendWithButtons(chatId,
            $"🏢 Редактирование: {apt.Title}\n" +
            $"───────────────────────\n" +
            $"📍 Адрес: {apt.Address}\n" +
            $"💰 Цена: {apt.PricePerNight:N0} ₽/ночь\n" +
            $"🛏 Комнат: {apt.RoomsCount}  👥 Гостей: {apt.Guests}  📐 {apt.Area} м²\n" +
            $"Статус: {(apt.IsActive ? "🟢 Активна" : "🔴 Скрыта")}\n\n" +
            $"Выберите поле для изменения:",
            buttons, ct);
    }

    async Task EditApartmentField(long chatId, AppDbContext db, string value, CancellationToken ct)
    {
        var d = _tempData[chatId];
        int aptId = int.Parse(d["apartmentId"]);
        var field = d["field"];

        var apt = await db.Apartments.FindAsync(aptId);
        if (apt == null) { await SendMenu(chatId, "❌ Квартира не найдена.", ct); return; }

        string changed;
        try
        {
            switch (field)
            {
                case "title": apt.Title = value; changed = $"Название → {value}"; break;
                case "address": apt.Address = value; changed = $"Адрес → {value}"; break;
                case "description": apt.Description = value; changed = "Описание обновлено"; break;
                case "price":
                    apt.PricePerNight = decimal.Parse(value.Replace(",", "."),
                                        System.Globalization.CultureInfo.InvariantCulture);
                    changed = $"Цена → {apt.PricePerNight:N0} ₽/ночь"; break;
                case "rooms": apt.RoomsCount = int.Parse(value); changed = $"Комнат → {value}"; break;
                case "guests": apt.Guests = int.Parse(value); changed = $"Гостей → {value}"; break;
                case "area":
                    apt.Area = decimal.Parse(value.Replace(",", "."),
                                        System.Globalization.CultureInfo.InvariantCulture);
                    changed = $"Площадь → {apt.Area} м²"; break;
                case "active":
                    apt.IsActive = bool.Parse(value);
                    changed = $"Статус → {(apt.IsActive ? "🟢 Активна" : "🔴 Скрыта")}"; break;
                default: await Send(chatId, "❌ Неизвестное поле.", ct); return;
            }
        }
        catch { await Send(chatId, "⚠️ Неверный формат значения. Попробуйте ещё раз:", ct); return; }

        await db.SaveChangesAsync(ct);
        _states.TryRemove(chatId, out _); _tempData.TryRemove(chatId, out _);
        await SendMenu(chatId, $"✅ Квартира обновлена!\n🏢 {apt.Title}\n{changed}", ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ОТПРАВКА СООБЩЕНИЙ — HTTP
    // ═══════════════════════════════════════════════════════════════════════

    async Task Send(long chatId, string text, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { text });
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?user_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Console.WriteLine($"[MaxBot] Send error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    async Task SendMenu(long chatId, string text, CancellationToken ct)
    {
        var menu =
            "📅 /bookings_today  |  🗓 /bookings_month\n" +
            "➕ /booking_create  |  🗑 /booking_delete\n" +
            "👤 /client_new      |  🔍 /client_search\n" +
            "📋 /client_info     |  📌 /client_bookings\n" +
            "🏢 /apartment_edit  |  ❌ /cancel";

        await Send(chatId, $"{text}\n\n─────────────\n{menu}", ct);
    }

    async Task SendWithButtons(long chatId, string text, List<object[]> buttonRows, CancellationToken ct)
    {
        var payload = new
        {
            text,
            attachments = new[]
            {
            new
            {
                type    = "inline_keyboard",
                payload = new { buttons = buttonRows }
            }
        }
        };

        var body = JsonSerializer.Serialize(payload);
        var resp = await _http.PostAsync(
            $"{BaseUrl}/messages?user_id={chatId}",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
            Console.WriteLine($"[MaxBot] SendWithButtons error {resp.StatusCode}: {await resp.Content.ReadAsStringAsync(ct)}");
    }

    async Task SendLong(long chatId, string text, CancellationToken ct)
    {
        const int maxLen = 4000;
        if (text.Length <= maxLen) { await Send(chatId, text, ct); return; }

        var lines = text.Split('\n');
        var chunk = new StringBuilder();
        foreach (var line in lines)
        {
            if (chunk.Length + line.Length + 1 > maxLen)
            {
                await Send(chatId, chunk.ToString(), ct);
                chunk.Clear();
            }
            chunk.AppendLine(line);
        }
        if (chunk.Length > 0) await Send(chatId, chunk.ToString(), ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ═══════════════════════════════════════════════════════════════════════

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
        return $"  #{b.Id} {StatusEmoji(b.Status)}  " +
               $"{b.StartDate:dd.MM} — {b.EndDate:dd.MM.yyyy}  🌙{nights}  💰{b.TotalPrice:N0}₽";
    }

    static string StatusEmoji(string status) => status switch
    {
        "Confirmed" => "✅",
        "Cancelled" => "❌",
        "Pending" => "⏳",
        _ => "❓"
    };

    static bool TryParseDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date);

    static string GetFieldHint(string field) => field switch
    {
        "active" => "Введите true (активна) или false (скрыта):",
        "price" => "Введите новую цену за ночь (число):",
        "rooms" => "Введите количество комнат:",
        "guests" => "Введите макс. количество гостей:",
        "area" => "Введите площадь (м²):",
        _ => $"Введите новое значение для поля \"{field}\":"
    };
}