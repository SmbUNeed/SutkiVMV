using Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SutkiVMV.Data;
using SutkiVMVCS.Data;

namespace SutkiVMVCS.Services.Shared;

public class CleanupStaleBookingsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CleanupStaleBookingsService> _logger;

    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _staleThreshold = TimeSpan.FromMinutes(30);

    public CleanupStaleBookingsService(
        IServiceScopeFactory scopeFactory,
        ILogger<CleanupStaleBookingsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var threshold = DateTime.UtcNow - _staleThreshold;

                // Находим все брони AwaitingPayment, созданные раньше порогового времени
                var staleBookings = await context.Bookings
                    .Where(b => b.Status == BookingStatus.AwaitingPayment && b.CreatedAt < threshold)
                    .ToListAsync(stoppingToken);

                if (staleBookings.Any())
                {
                    // ── ВАРИАНТ А (Рекомендуемый): Мягкое удаление (Отмена) ──
                    // Сохраняет историю и не ломает статистику/логи
                    foreach (var booking in staleBookings)
                    {
                        booking.Status = BookingStatus.Canceled;
                    }

                    // ── ВАРИАНТ Б: Физическое удаление ──
                    // Раскомментируй, если уверен, что каскадное удаление не сломает связанные Payment
                    // context.Bookings.RemoveRange(staleBookings);

                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Очищено {Count} зависших броней со статусом AwaitingPayment", staleBookings.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке зависших броней");
            }

            // Ждем до следующей проверки
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}