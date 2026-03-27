/*using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramBotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly long _adminId;

    public TelegramBotWorker(ITelegramBotClient botClient, IConfiguration config, IServiceProvider serviceProvider)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _adminId = long.Parse(config["TelegramBot:AdminChatId"]!);
    }

    // ── Состояние диалога для каждого пользователя ──────────────────────────
    private enum BotState
    {
        None,

        // Брони
        BookingToday,
        BookingMonth,

        CreateBooking_WaitApartment,
        CreateBooking_WaitClient,
        CreateBooking_WaitStartDate,
        CreateBooking_WaitEndDate,

        DeleteBooking_WaitId,

        // Клиенты
        NewClient_WaitName,
        NewClient_WaitPhone,
        NewClient_WaitEmail,

        ClientSearch_WaitQuery,
        ClientInfo_WaitId,
        ClientBookings_WaitId,

        // Квартира
        EditApartment_WaitId,
        EditApartment_WaitField,
        EditApartment_WaitValue,
    }

    private readonly ConcurrentDictionary<long, BotState> _states = new();
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, string>> _tempData = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _botClient.SetMyCommands(new[]
            {
                new BotCommand { Command = "start",            Description = "🏠 Главное меню" },
                new BotCommand { Command = "bookings_today",   Description = "📅 Брони на сегодня" },
                new BotCommand { Command = "bookings_month",   Description = "🗓 Брони на месяц" },
                new BotCommand { Command = "booking_create",   Description = "➕ Создать бронь" },
                new BotCommand { Command = "booking_delete",   Description = "🗑 Удалить бронь" },
                new BotCommand { Command = "client_new",       Description = "👤 Новый клиент" },
                new BotCommand { Command = "client_search",    Description = "🔍 Поиск клиента" },
                new BotCommand { Command = "client_info",      Description = "📋 Данные клиента" },
                new BotCommand { Command = "client_bookings",  Description = "📌 Брони клиента" },
                new BotCommand { Command = "apartment_edit",   Description = "🏢 Редактировать квартиру" },
                new BotCommand { Command = "cancel",           Description = "❌ Отменить действие" },
            }, cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TgBot] SetMyCommands failed: {ex.Message}");
            // не падаем — продолжаем работу
        }

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
            },
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ГЛАВНЫЙ ОБРАБОТЧИК
    // ═══════════════════════════════════════════════════════════════════════

    async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message is { Text: not null } msg)
            {
                if (msg.Chat.Id != _adminId) return;
                await HandleMessage(bot, msg, ct);
            }
            else if (update.CallbackQuery is { Data: not null } cb)
            {
                if (cb.From.Id != _adminId) return;
                await HandleCallback(bot, cb, ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Bot] Ошибка: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ОБРАБОТКА СООБЩЕНИЙ
    // ═══════════════════════════════════════════════════════════════════════

    async Task HandleMessage(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var text = msg.Text!.Trim();
        var state = _states.GetValueOrDefault(chatId, BotState.None);

        // /cancel — всегда работает
        if (text == "/cancel")
        {
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            await SendMenu(bot, chatId, "❌ Действие отменено.", ct);
            return;
        }

        // Кнопки Reply-клавиатуры → маппинг в команды
        var keyboardMap = new Dictionary<string, string>
        {
            ["📅 Брони сегодня"] = "/bookings_today",
            ["🗓 Брони на месяц"] = "/bookings_month",
            ["➕ Создать бронь"] = "/booking_create",
            ["🗑 Удалить бронь"] = "/booking_delete",
            ["👤 Новый клиент"] = "/client_new",
            ["🔍 Поиск клиента"] = "/client_search",
            ["📋 Данные клиента"] = "/client_info",
            ["📌 Брони клиента"] = "/client_bookings",
            ["🏢 Редактировать квартиру"] = "/apartment_edit",
        };

        if (keyboardMap.TryGetValue(text, out var mappedCmd))
        {
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            await HandleCommand(bot, chatId, mappedCmd, ct);
            return;
        }

        // Команды меню
        if (text.StartsWith('/'))
        {
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            await HandleCommand(bot, chatId, text, ct);
            return;
        }

        // Обработка по состоянию
        await HandleByState(bot, chatId, text, state, ct);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  КОМАНДЫ
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleCommand(ITelegramBotClient bot, long chatId, string cmd, CancellationToken ct)
    {
        // убрать возможные @BotName суффиксы
        cmd = cmd.Split('@')[0].ToLower();

        switch (cmd)
        {
            case "/start":
                await SendMenu(bot, chatId, "👋 Добро пожаловать в панель управления!", ct);
                break;

            case "/bookings_today":
                await ShowBookingsToday(bot, chatId, ct);
                break;

            case "/bookings_month":
                await ShowBookingsMonth(bot, chatId, ct);
                break;

            case "/booking_create":
                _states[chatId] = BotState.CreateBooking_WaitApartment;
                await ShowApartmentsList(bot, chatId, ct);
                break;

            case "/booking_delete":
                _states[chatId] = BotState.DeleteBooking_WaitId;
                await bot.SendMessage(chatId,
                    "🗑 <b>Удаление брони</b>\n\nВведите <b>ID брони</b> для удаления (отмена: /cancel):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case "/client_new":
                _states[chatId] = BotState.NewClient_WaitName;
                _tempData[chatId] = new ConcurrentDictionary<string, string>();
                await bot.SendMessage(chatId,
                    "👤 <b>Новый клиент</b>\n\nВведите <b>ФИО</b> клиента (отмена: /cancel):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case "/client_search":
                _states[chatId] = BotState.ClientSearch_WaitQuery;
                await bot.SendMessage(chatId,
                    "🔍 <b>Поиск клиента</b>\n\nВведите имя, телефон или email (отмена: /cancel):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case "/client_info":
                _states[chatId] = BotState.ClientInfo_WaitId;
                await bot.SendMessage(chatId,
                    "📋 <b>Данные клиента</b>\n\nВведите <b>ID клиента</b> (отмена: /cancel):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case "/client_bookings":
                _states[chatId] = BotState.ClientBookings_WaitId;
                await bot.SendMessage(chatId,
                    "📌 <b>Брони клиента</b>\n\nВведите <b>ID клиента</b> (отмена: /cancel):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case "/apartment_edit":
                _states[chatId] = BotState.EditApartment_WaitId;
                await ShowApartmentsList(bot, chatId, ct);
                break;

            default:
                await SendMenu(bot, chatId, "❓ Неизвестная команда.", ct);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ОБРАБОТКА ПО СОСТОЯНИЮ
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleByState(ITelegramBotClient bot, long chatId, string text, BotState state, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (state)
        {
            // ── СОЗДАНИЕ БРОНИ ───────────────────────────────────────────

            case BotState.CreateBooking_WaitApartment:
                if (!int.TryParse(text, out int aptId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID квартиры</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                var apt = await db.Apartments.FindAsync(aptId);
                if (apt == null || apt.IsDeleted)
                {
                    await bot.SendMessage(chatId, "❌ Квартира не найдена. Попробуйте ещё раз:", cancellationToken: ct);
                    return;
                }
                _tempData[chatId] = new ConcurrentDictionary<string, string>()
                { ["apartmentId"] = aptId.ToString(), ["apartmentTitle"] = apt.Title };
                _states[chatId] = BotState.CreateBooking_WaitClient;
                await ShowClientsList(bot, chatId, db, ct);
                break;

            case BotState.CreateBooking_WaitClient:
                if (!int.TryParse(text, out int clientId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID клиента</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                var client = await db.Clients.FindAsync(clientId);
                if (client == null)
                {
                    await bot.SendMessage(chatId, "❌ Клиент не найден. Попробуйте ещё раз:", cancellationToken: ct);
                    return;
                }
                _tempData[chatId]["clientId"] = clientId.ToString();
                _tempData[chatId]["clientName"] = client.FullName;
                _states[chatId] = BotState.CreateBooking_WaitStartDate;
                await bot.SendMessage(chatId,
                    $"✅ Клиент: <b>{client.FullName}</b>\n\n📅 Введите <b>дату заезда</b> в формате ДД.ММ.ГГГГ:",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case BotState.CreateBooking_WaitStartDate:
                if (!TryParseDate(text, out DateTime start))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверный формат. Введите дату <b>ДД.ММ.ГГГГ</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                _tempData[chatId]["startDate"] = start.ToString("O");
                _states[chatId] = BotState.CreateBooking_WaitEndDate;
                await bot.SendMessage(chatId,
                    $"✅ Дата заезда: <b>{start:dd.MM.yyyy}</b>\n\n📅 Введите <b>дату выезда</b> в формате ДД.ММ.ГГГГ:",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case BotState.CreateBooking_WaitEndDate:
                if (!TryParseDate(text, out DateTime end))
                {
                    await bot.SendMessage(chatId, "⚠️ Неверный формат. Введите дату <b>ДД.ММ.ГГГГ</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                var startDate = DateTime.Parse(_tempData[chatId]["startDate"]);
                if (end <= startDate)
                {
                    await bot.SendMessage(chatId, "⚠️ Дата выезда должна быть позже даты заезда. Попробуйте ещё раз:", cancellationToken: ct);
                    return;
                }
                await CreateBooking(bot, chatId, db, startDate, end, ct);
                break;

            // ── УДАЛЕНИЕ БРОНИ ───────────────────────────────────────────

            case BotState.DeleteBooking_WaitId:
                if (!int.TryParse(text, out int bookingId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID брони</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                await DeleteBooking(bot, chatId, db, bookingId, ct);
                break;

            // ── НОВЫЙ КЛИЕНТ ─────────────────────────────────────────────

            case BotState.NewClient_WaitName:
                if (text.Length < 2)
                {
                    await bot.SendMessage(chatId, "⚠️ Имя слишком короткое. Введите ФИО:", cancellationToken: ct);
                    return;
                }
                _tempData[chatId]["name"] = text;
                _states[chatId] = BotState.NewClient_WaitPhone;
                await bot.SendMessage(chatId,
                    $"✅ ФИО: <b>{text}</b>\n\n📞 Введите <b>номер телефона</b>:",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case BotState.NewClient_WaitPhone:
                _tempData[chatId]["phone"] = text;
                _states[chatId] = BotState.NewClient_WaitEmail;
                await bot.SendMessage(chatId,
                    $"✅ Телефон: <b>{text}</b>\n\n✉️ Введите <b>email</b> (или прочерк —):",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case BotState.NewClient_WaitEmail:
                _tempData[chatId]["email"] = text == "-" ? "" : text;
                await CreateClient(bot, chatId, db, ct);
                break;

            // ── ПОИСК КЛИЕНТА ────────────────────────────────────────────

            case BotState.ClientSearch_WaitQuery:
                await SearchClients(bot, chatId, db, text, ct);
                break;

            // ── ИНФОРМАЦИЯ О КЛИЕНТЕ ─────────────────────────────────────

            case BotState.ClientInfo_WaitId:
                if (!int.TryParse(text, out int cId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID клиента</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                await ShowClientInfo(bot, chatId, db, cId, ct);
                break;

            // ── БРОНИ КЛИЕНТА ────────────────────────────────────────────

            case BotState.ClientBookings_WaitId:
                if (!int.TryParse(text, out int cbId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID клиента</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                await ShowClientBookings(bot, chatId, db, cbId, ct);
                break;

            // ── РЕДАКТИРОВАНИЕ КВАРТИРЫ ──────────────────────────────────

            case BotState.EditApartment_WaitId:
                if (!int.TryParse(text, out int eAptId))
                {
                    await bot.SendMessage(chatId, "⚠️ Введите корректный <b>ID квартиры</b>:", parseMode: ParseMode.Html, cancellationToken: ct);
                    return;
                }
                var eApt = await db.Apartments.FindAsync(eAptId);
                if (eApt == null || eApt.IsDeleted)
                {
                    await bot.SendMessage(chatId, "❌ Квартира не найдена.", cancellationToken: ct);
                    return;
                }
                _tempData[chatId] = new ConcurrentDictionary<string, string>()
                { ["apartmentId"] = eAptId.ToString() };
                _states[chatId] = BotState.EditApartment_WaitField;
                await ShowEditApartmentMenu(bot, chatId, eApt, ct);
                break;

            case BotState.EditApartment_WaitField:
                var validFields = new[] { "title", "address", "description", "price", "rooms", "guests", "area", "active" };
                if (!validFields.Contains(text.ToLower()))
                {
                    await bot.SendMessage(chatId, "⚠️ Выберите поле из списка выше или нажмите кнопку:", cancellationToken: ct);
                    return;
                }
                _tempData[chatId]["field"] = text.ToLower();
                _states[chatId] = BotState.EditApartment_WaitValue;
                var fieldHint = text.ToLower() switch
                {
                    "active" => "Введите <b>true</b> (активна) или <b>false</b> (скрыта):",
                    "price" => "Введите новую <b>цену за ночь</b> (число):",
                    "rooms" => "Введите <b>количество комнат</b>:",
                    "guests" => "Введите <b>макс. кол-во гостей</b>:",
                    "area" => "Введите <b>площадь</b> (м²):",
                    _ => $"Введите новое значение для <b>{text}</b>:"
                };
                await bot.SendMessage(chatId, fieldHint, parseMode: ParseMode.Html, cancellationToken: ct);
                break;

            case BotState.EditApartment_WaitValue:
                await EditApartmentField(bot, chatId, db, text, ct);
                break;

            default:
                await SendMenu(bot, chatId, "🏠 Используйте меню или команды ниже.", ct);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ОБРАБОТКА CALLBACK КНОПОК
    // ─────────────────────────────────────────────────────────────────────

    async Task HandleCallback(ITelegramBotClient bot, CallbackQuery cb, CancellationToken ct)
    {
        var chatId = cb.From.Id;
        var data = cb.Data!;

        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Подтвердить / Отменить бронь ──────────────────────────────
        if (data.StartsWith("confirm_") || data.StartsWith("cancel_"))
        {
            var parts = data.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out int bId))
            {
                var booking = await db.Bookings
                    .Include(b => b.Apartment)
                    .Include(b => b.Client)
                    .FirstOrDefaultAsync(b => b.Id == bId, ct);

                if (booking == null)
                {
                    await bot.AnswerCallbackQuery(cb.Id, "❌ Бронь не найдена!", showAlert: true, cancellationToken: ct);
                    return;
                }

                string newStatus = parts[0] == "confirm" ? "Confirmed" : "Cancelled";
                string statusText = newStatus == "Confirmed" ? "✅ Подтверждена" : "❌ Отменена";
                booking.Status = newStatus;
                await db.SaveChangesAsync(ct);

                if (cb.Message is { } msg)
                {
                    await bot.EditMessageText(
                        chatId: msg.Chat.Id, messageId: msg.MessageId,
                        text: $"{msg.Text}\n\n<b>Статус изменён:</b> {statusText}",
                        parseMode: ParseMode.Html, replyMarkup: null, cancellationToken: ct);
                }
            }
            return;
        }

        // ── Выбор поля для редактирования квартиры ────────────────────
        if (data.StartsWith("editfield_"))
        {
            var field = data.Replace("editfield_", "");
            _tempData[chatId]["field"] = field;
            _states[chatId] = BotState.EditApartment_WaitValue;

            var hint = field switch
            {
                "title" => "Введите новое <b>название</b>:",
                "address" => "Введите новый <b>адрес</b>:",
                "description" => "Введите новое <b>описание</b>:",
                "price" => "Введите новую <b>цену за ночь</b> (число):",
                "rooms" => "Введите <b>количество комнат</b>:",
                "guests" => "Введите <b>макс. кол-во гостей</b>:",
                "area" => "Введите <b>площадь</b> (м²):",
                "active" => "Введите <b>true</b> (активна) или <b>false</b> (скрыта):",
                _ => "Введите новое значение:"
            };
            await bot.SendMessage(chatId, hint, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // ── Выбор квартиры при создании брони ────────────────────────
        if (data.StartsWith("apt_"))
        {
            if (int.TryParse(data.Replace("apt_", ""), out int aId))
            {
                var apt = await db.Apartments.FindAsync(aId, ct);
                if (apt != null)
                {
                    var state = _states.GetValueOrDefault(chatId, BotState.None);
                    _tempData[chatId] = _tempData.GetValueOrDefault(chatId, new());
                    _tempData[chatId]["apartmentId"] = aId.ToString();
                    _tempData[chatId]["apartmentTitle"] = apt.Title;

                    if (state == BotState.EditApartment_WaitId)
                    {
                        _states[chatId] = BotState.EditApartment_WaitField;
                        await ShowEditApartmentMenu(bot, chatId, apt, ct);
                    }
                    else
                    {
                        _states[chatId] = BotState.CreateBooking_WaitClient;
                        await ShowClientsList(bot, chatId, db, ct);
                    }
                }
            }
            return;
        }

        // ── Выбор клиента при создании брони ─────────────────────────
        if (data.StartsWith("cli_"))
        {
            if (int.TryParse(data.Replace("cli_", ""), out int cId))
            {
                var client = await db.Clients.FindAsync(cId, ct);
                if (client != null)
                {
                    _tempData[chatId]["clientId"] = cId.ToString();
                    _tempData[chatId]["clientName"] = client.FullName;
                    _states[chatId] = BotState.CreateBooking_WaitStartDate;
                    await bot.SendMessage(chatId,
                        $"✅ Клиент: <b>{client.FullName}</b>\n\n📅 Введите <b>дату заезда</b> (ДД.ММ.ГГГГ):",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                }
            }
            return;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  МЕТОДЫ-ДЕЙСТВИЯ
    // ═══════════════════════════════════════════════════════════════════════

    async Task ShowBookingsToday(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var bookings = await db.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .Where(b => b.StartDate < tomorrow && b.EndDate > today && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title)
            .AsNoTracking()
            .ToListAsync(ct);

        if (!bookings.Any())
        {
            await SendMenu(bot, chatId, "📅 На сегодня нет активных броней.", ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📅 <b>Брони на сегодня</b> ({today:dd.MM.yyyy}) — {bookings.Count} шт.\n");

        foreach (var b in bookings)
            sb.AppendLine(FormatBooking(b));

        await SendLongMessage(bot, chatId, sb.ToString(), ct);
    }

    async Task ShowBookingsMonth(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(30);

        var bookings = await db.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .Where(b => b.StartDate < end && b.EndDate > start && b.Status != "Cancelled")
            .OrderBy(b => b.Apartment.Title)
            .ThenBy(b => b.StartDate)
            .AsNoTracking()
            .ToListAsync(ct);

        if (!bookings.Any())
        {
            await SendMenu(bot, chatId, "🗓 На ближайший месяц броней нет.", ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🗓 <b>Брони на 30 дней</b> ({start:dd.MM} — {end:dd.MM.yyyy}) — {bookings.Count} шт.\n");

        string? lastApt = null;
        foreach (var b in bookings)
        {
            if (b.Apartment?.Title != lastApt)
            {
                lastApt = b.Apartment?.Title;
                sb.AppendLine($"\n🏢 <b>{lastApt}</b>");
            }
            sb.AppendLine(FormatBookingShort(b));
        }

        await SendLongMessage(bot, chatId, sb.ToString(), ct);
    }

    async Task ShowApartmentsList(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = _states.GetValueOrDefault(chatId, BotState.None);
        string header = state == BotState.EditApartment_WaitId
            ? "🏢 <b>Выберите квартиру для редактирования:</b>\n"
            : "🏢 <b>Выберите квартиру для брони:</b>\n";

        var apts = await db.Apartments
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Title)
            .AsNoTracking()
            .ToListAsync(ct);

        if (!apts.Any())
        {
            await bot.SendMessage(chatId, "❌ Нет доступных квартир.", cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder(header + "\n");
        foreach (var a in apts)
            sb.AppendLine($"  <code>{a.Id}</code> · {(a.IsActive ? "🟢" : "🔴")} <b>{a.Title}</b> — {a.PricePerNight:N0} ₽/ночь");

        sb.AppendLine("\nВведите <b>ID</b> квартиры:");

        // Inline-кнопки для удобства (до 8 квартир)
        var buttons = apts.Take(8)
            .Select(a => InlineKeyboardButton.WithCallbackData(
                $"{(a.IsActive ? "🟢" : "🔴")} {a.Title}", $"apt_{a.Id}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Select(b => new[] { b }));
        await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html,
            replyMarkup: keyboard, cancellationToken: ct);
    }

    async Task ShowClientsList(ITelegramBotClient bot, long chatId, AppDbContext db, CancellationToken ct)
    {
        var clients = await db.Clients
            .OrderBy(c => c.FullName)
            .Take(30)
            .AsNoTracking()
            .ToListAsync(ct);

        var aptTitle = _tempData[chatId].GetValueOrDefault("apartmentTitle", "");
        var sb = new System.Text.StringBuilder($"👤 <b>Выберите клиента</b> для «{aptTitle}»:\n\n");

        foreach (var c in clients)
            sb.AppendLine($"  <code>{c.Id}</code> · <b>{c.FullName}</b>  {c.PhoneNumber}");

        sb.AppendLine("\nВведите <b>ID</b> клиента:");

        var buttons = clients.Take(8)
            .Select(c => InlineKeyboardButton.WithCallbackData(
                $"{c.FullName}", $"cli_{c.Id}"))
            .ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons.Select(b => new[] { b }));
        await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html,
            replyMarkup: keyboard, cancellationToken: ct);
    }

    async Task CreateBooking(ITelegramBotClient bot, long chatId, AppDbContext db,
        DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var d = _tempData[chatId];
        int apartmentId = int.Parse(d["apartmentId"]);
        int clientId = int.Parse(d["clientId"]);

        var apartment = await db.Apartments.FindAsync(apartmentId);
        var client = await db.Clients.FindAsync(clientId);

        if (apartment == null || client == null)
        {
            await bot.SendMessage(chatId, "❌ Ошибка: квартира или клиент не найдены.", cancellationToken: ct);
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            return;
        }

        bool overlap = await db.Bookings.AnyAsync(b =>
            b.ApartmentId == apartmentId &&
            b.Status != "Cancelled" &&
            b.StartDate < endDate.ToUniversalTime() &&
            b.EndDate > startDate.ToUniversalTime(), ct);

        if (overlap)
        {
            await bot.SendMessage(chatId,
                "⚠️ <b>Квартира занята</b> в выбранные даты.\n\nВведите другую дату заезда (ДД.ММ.ГГГГ):",
                parseMode: ParseMode.Html, cancellationToken: ct);
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

        _states.TryRemove(chatId, out _);
        _tempData.TryRemove(chatId, out _);

        var msg = $"✅ <b>Бронь создана!</b>\n\n" +
                  $"🏢 Квартира: <b>{apartment.Title}</b>\n" +
                  $"👤 Клиент: <b>{client.FullName}</b>  {client.PhoneNumber}\n" +
                  $"📅 Заезд: <b>{startDate:dd.MM.yyyy}</b>\n" +
                  $"📅 Выезд: <b>{endDate:dd.MM.yyyy}</b>\n" +
                  $"🌙 Ночей: <b>{nights}</b>\n" +
                  $"💰 Итого: <b>{booking.TotalPrice:N0} ₽</b>\n" +
                  $"🆔 Бронь #<b>{booking.Id}</b>";

        await SendMenu(bot, chatId, msg, ct);
    }

    async Task DeleteBooking(ITelegramBotClient bot, long chatId, AppDbContext db, int bookingId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            await bot.SendMessage(chatId,
                "❌ Бронь не найдена. Введите другой ID или /cancel:",
                cancellationToken: ct);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить отмену", $"cancel_{bookingId}"),
                InlineKeyboardButton.WithCallbackData("↩️ Назад", "menu_back")
            }
        });

        _states.TryRemove(chatId, out _);
        _tempData.TryRemove(chatId, out _);

        await bot.SendMessage(chatId,
            $"🗑 <b>Удаление брони #{bookingId}</b>\n\n" +
            $"🏢 {booking.Apartment?.Title}\n" +
            $"👤 {booking.Client?.FullName}\n" +
            $"📅 {booking.StartDate:dd.MM.yyyy} — {booking.EndDate:dd.MM.yyyy}\n" +
            $"💰 {booking.TotalPrice:N0} ₽  |  Статус: {StatusEmoji(booking.Status)}\n\n" +
            $"Подтвердите отмену брони:",
            parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
    }

    async Task CreateClient(ITelegramBotClient bot, long chatId, AppDbContext db, CancellationToken ct)
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

        _states.TryRemove(chatId, out _);
        _tempData.TryRemove(chatId, out _);

        await SendMenu(bot, chatId,
            $"✅ <b>Клиент создан!</b>\n\n" +
            $"🆔 ID: <b>{client.Id}</b>\n" +
            $"👤 ФИО: <b>{client.FullName}</b>\n" +
            $"📞 Тел: <b>{client.PhoneNumber}</b>\n" +
            $"✉️ Email: <b>{(string.IsNullOrEmpty(client.Email) ? "—" : client.Email)}</b>", ct);
    }

    async Task SearchClients(ITelegramBotClient bot, long chatId, AppDbContext db, string query, CancellationToken ct)
    {
        var q = query.Trim().ToLower();
        var clients = await db.Clients
            .Where(c => c.FullName.ToLower().Contains(q)
                     || c.PhoneNumber.Contains(q)
                     || c.Email.ToLower().Contains(q))
            .OrderBy(c => c.FullName)
            .Take(20)
            .Include(c => c.Bookings)
            .AsNoTracking()
            .ToListAsync(ct);

        _states.TryRemove(chatId, out _);

        if (!clients.Any())
        {
            await SendMenu(bot, chatId, $"🔍 По запросу «{query}» ничего не найдено.", ct);
            return;
        }

        var sb = new System.Text.StringBuilder($"🔍 <b>Найдено клиентов:</b> {clients.Count}\n\n");
        foreach (var c in clients)
        {
            var bookingsCount = c.Bookings.Count;
            sb.AppendLine($"<code>{c.Id}</code> · <b>{c.FullName}</b>");
            sb.AppendLine($"    📞 {c.PhoneNumber}  {(c.Email != "" ? $"✉️ {c.Email}" : "")}");
            sb.AppendLine($"    📌 Броней: {bookingsCount}");
            sb.AppendLine();
        }

        await SendLongMessage(bot, chatId, sb.ToString(), ct);
    }

    async Task ShowClientInfo(ITelegramBotClient bot, long chatId, AppDbContext db, int clientId, CancellationToken ct)
    {
        var client = await db.Clients
            .Include(c => c.Bookings)
            .ThenInclude(b => b.Apartment)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        _states.TryRemove(chatId, out _);

        if (client == null)
        {
            await SendMenu(bot, chatId, "❌ Клиент не найден.", ct);
            return;
        }

        var totalSpent = client.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice);
        var activeCount = client.Bookings.Count(b => b.Status != "Cancelled");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"👤 <b>Клиент #{client.Id}</b>");
        sb.AppendLine($"───────────────────────");
        sb.AppendLine($"ФИО: <b>{client.FullName}</b>");
        sb.AppendLine($"Тел: <b>{client.PhoneNumber}</b>");
        sb.AppendLine($"Email: <b>{(string.IsNullOrEmpty(client.Email) ? "—" : client.Email)}</b>");
        sb.AppendLine($"");
        sb.AppendLine($"📌 Всего броней: <b>{client.Bookings.Count}</b>");
        sb.AppendLine($"✅ Активных: <b>{activeCount}</b>");
        sb.AppendLine($"💰 Потрачено: <b>{totalSpent:N0} ₽</b>");

        await SendMenu(bot, chatId, sb.ToString(), ct);
    }

    async Task ShowClientBookings(ITelegramBotClient bot, long chatId, AppDbContext db, int clientId, CancellationToken ct)
    {
        var client = await db.Clients
            .Include(c => c.Bookings).ThenInclude(b => b.Apartment)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        _states.TryRemove(chatId, out _);

        if (client == null)
        {
            await SendMenu(bot, chatId, "❌ Клиент не найден.", ct);
            return;
        }

        var bookings = client.Bookings
            .OrderBy(b => b.Apartment?.Title)
            .ThenByDescending(b => b.StartDate)
            .ToList();

        if (!bookings.Any())
        {
            await SendMenu(bot, chatId, $"📌 У клиента <b>{client.FullName}</b> нет броней.", ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📌 <b>Брони клиента: {client.FullName}</b> ({bookings.Count} шт.)\n");

        string? lastApt = null;
        foreach (var b in bookings)
        {
            if (b.Apartment?.Title != lastApt)
            {
                lastApt = b.Apartment?.Title;
                sb.AppendLine($"\n🏢 <b>{lastApt}</b>");
            }
            sb.AppendLine(FormatBookingShort(b));
        }

        await SendLongMessage(bot, chatId, sb.ToString(), ct);
    }

    async Task ShowEditApartmentMenu(ITelegramBotClient bot, long chatId, Apartment apt, CancellationToken ct)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📝 Название",   "editfield_title"),
                    InlineKeyboardButton.WithCallbackData("📍 Адрес",       "editfield_address") },
            new[] { InlineKeyboardButton.WithCallbackData("💰 Цена",       "editfield_price"),
                    InlineKeyboardButton.WithCallbackData("🛏 Комнаты",     "editfield_rooms") },
            new[] { InlineKeyboardButton.WithCallbackData("👥 Гости",      "editfield_guests"),
                    InlineKeyboardButton.WithCallbackData("📐 Площадь",    "editfield_area") },
            new[] { InlineKeyboardButton.WithCallbackData("🔤 Описание",   "editfield_description") },
            new[] { InlineKeyboardButton.WithCallbackData(apt.IsActive ? "🔴 Скрыть" : "🟢 Активировать", "editfield_active") },
        });

        await bot.SendMessage(chatId,
            $"🏢 <b>Редактирование: {apt.Title}</b>\n" +
            $"───────────────────────\n" +
            $"📍 Адрес: {apt.Address}\n" +
            $"💰 Цена: {apt.PricePerNight:N0} ₽/ночь\n" +
            $"🛏 Комнат: {apt.RoomsCount}  👥 Гостей: {apt.Guests}  📐 {apt.Area} м²\n" +
            $"Статус: {(apt.IsActive ? "🟢 Активна" : "🔴 Скрыта")}\n\n" +
            $"Выберите поле для изменения:",
            parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
    }

    async Task EditApartmentField(ITelegramBotClient bot, long chatId, AppDbContext db, string value, CancellationToken ct)
    {
        var d = _tempData[chatId];
        int aptId = int.Parse(d["apartmentId"]);
        var field = d["field"];

        var apt = await db.Apartments.FindAsync(aptId);
        if (apt == null)
        {
            await SendMenu(bot, chatId, "❌ Квартира не найдена.", ct);
            _states.TryRemove(chatId, out _);
            _tempData.TryRemove(chatId, out _);
            return;
        }

        string changed = "";
        try
        {
            switch (field)
            {
                case "title": apt.Title = value; changed = $"Название → {value}"; break;
                case "address": apt.Address = value; changed = $"Адрес → {value}"; break;
                case "description": apt.Description = value; changed = $"Описание обновлено"; break;
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
                case "active": apt.IsActive = bool.Parse(value); changed = $"Статус → {(apt.IsActive ? "🟢 Активна" : "🔴 Скрыта")}"; break;
                default:
                    await bot.SendMessage(chatId, "❌ Неизвестное поле.", cancellationToken: ct);
                    return;
            }
        }
        catch
        {
            await bot.SendMessage(chatId, "⚠️ Неверный формат значения. Попробуйте ещё раз:", cancellationToken: ct);
            return;
        }

        await db.SaveChangesAsync(ct);
        _states.TryRemove(chatId, out _);
        _tempData.TryRemove(chatId, out _);

        await SendMenu(bot, chatId,
            $"✅ <b>Квартира обновлена!</b>\n🏢 {apt.Title}\n{changed}", ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ═══════════════════════════════════════════════════════════════════════

    async Task SendMenu(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📅 Брони сегодня",   "🗓 Брони на месяц"   },
            new KeyboardButton[] { "➕ Создать бронь",    "🗑 Удалить бронь"    },
            new KeyboardButton[] { "👤 Новый клиент",     "🔍 Поиск клиента"   },
            new KeyboardButton[] { "📋 Данные клиента",  "📌 Брони клиента"    },
            new KeyboardButton[] { "🏢 Редактировать квартиру"                 },
        })
        { ResizeKeyboard = true };

        // Обработка кнопок клавиатуры
        var menuMap = new Dictionary<string, string>
        {
            ["📅 Брони сегодня"] = "/bookings_today",
            ["🗓 Брони на месяц"] = "/bookings_month",
            ["➕ Создать бронь"] = "/booking_create",
            ["🗑 Удалить бронь"] = "/booking_delete",
            ["👤 Новый клиент"] = "/client_new",
            ["🔍 Поиск клиента"] = "/client_search",
            ["📋 Данные клиента"] = "/client_info",
            ["📌 Брони клиента"] = "/client_bookings",
            ["🏢 Редактировать квартиру"] = "/apartment_edit",
        };

        await bot.SendMessage(chatId, text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    string FormatBooking(Booking b)
    {
        var nights = (b.EndDate - b.StartDate).Days;
        return $"🆔 #{b.Id}  {StatusEmoji(b.Status)}\n" +
               $"  🏢 <b>{b.Apartment?.Title ?? "—"}</b>\n" +
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

    static bool TryParseDate(string text, out DateTime date)
    {
        return DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date);
    }

    async Task SendLongMessage(ITelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        const int maxLen = 4000;
        if (text.Length <= maxLen)
        {
            await bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }
        // Разбиваем на части по строкам
        var lines = text.Split('\n');
        var chunk = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (chunk.Length + line.Length + 1 > maxLen)
            {
                await bot.SendMessage(chatId, chunk.ToString(), parseMode: ParseMode.Html, cancellationToken: ct);
                chunk.Clear();
            }
            chunk.AppendLine(line);
        }
        if (chunk.Length > 0)
            await bot.SendMessage(chatId, chunk.ToString(), parseMode: ParseMode.Html, cancellationToken: ct);
    }

    Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"[Bot] Ошибка: {ex.Message}");
        return Task.CompletedTask;
    }
}
*/