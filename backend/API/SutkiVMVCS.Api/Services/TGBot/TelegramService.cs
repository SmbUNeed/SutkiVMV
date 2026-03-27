/*using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups; // Важный using для кнопок!

public interface ITelegramService
{
    Task SendNotificationAsync(string message);
    Task SendBookingNotificationAsync(string message, int bookingId); // Новый метод
}

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _botClient;
    private readonly long _adminChatId;

    public TelegramService(ITelegramBotClient botClient, IConfiguration config)
    {
        _botClient = botClient;  // ← из DI
        _adminChatId = long.Parse(config["TelegramBot:AdminChatId"]!);
    }

    public async Task SendNotificationAsync(string message)
    {
        await _botClient.SendMessage(_adminChatId, message, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
    }

    // Новый метод с кнопками
    public async Task SendBookingNotificationAsync(string message, int bookingId)
    {
        // Создаем две кнопки в один ряд
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                // В CallbackData передаем команду и ID брони
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"confirm_{bookingId}"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить", $"cancel_{bookingId}")
            }
        });

        await _botClient.SendMessage(
            chatId: _adminChatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: inlineKeyboard // Прикрепляем кнопки
        );
    }
}*/