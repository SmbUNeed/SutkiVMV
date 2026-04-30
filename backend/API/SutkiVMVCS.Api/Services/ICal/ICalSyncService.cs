using SutkiVMV.Data;
using Microsoft.EntityFrameworkCore;

namespace SutkiVMV.Services;

public class ICalSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ICalSyncService> _logger;

    public ICalSyncService(IServiceProvider services, ILogger<ICalSyncService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var icalSvc = scope.ServiceProvider.GetRequiredService<ICalService>();

                var calendars = await db.ExternalCalendars.ToListAsync(ct);
                foreach (var cal in calendars)
                    await icalSvc.ImportAsync(cal);

                _logger.LogInformation("iCal sync completed: {count} calendars", calendars.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iCal sync error");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}