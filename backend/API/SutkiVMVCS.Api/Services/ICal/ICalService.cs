using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using SutkiVMV.Data;
using SutkiVMV.Models;
using Microsoft.EntityFrameworkCore;
using SutkiVMVCS.Services.Shared;

namespace SutkiVMV.Services;

public class ICalService
{
    private readonly AppDbContext _context;
    private readonly FcmService _fcm;
    private readonly IMaxService _max;

    public ICalService(AppDbContext context, FcmService fcm, IMaxService max)
    {
        _context = context;
        _fcm = fcm;
        _max = max;
    }

    public async Task<string> ExportAsync(int apartmentId)
    {
        var apt = await _context.Apartments
            .Include(a => a.Bookings)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == apartmentId);

        if (apt == null) return "";

        var calendar = new Calendar();

        foreach (var b in apt.Bookings.Where(b => b.Status != "Cancelled"))
        {
            calendar.Events.Add(new CalendarEvent
            {
                Uid = $"booking-{b.Id}@sutkivmv.ru",
                Summary = "Занято",
                Start = new CalDateTime(b.StartDate, "UTC"),
                End = new CalDateTime(b.EndDate, "UTC"),
            });
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? "";
    }

    public async Task ImportAsync(ExternalCalendar extCal)
    {
        using var http = new HttpClient();
        string icsContent;
        try { icsContent = await http.GetStringAsync(extCal.ICalUrl); }
        catch { return; }

        var calendar = Calendar.Load(icsContent);
        if (calendar == null) return;

        var newBookings = new List<string>(); // для уведомлений
        var changedBookings = new List<string>();

        var local = await _context.Bookings
            .Where(b => b.ApartmentId == extCal.ApartmentId && b.Status != "Cancelled")
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync();
        var keepUids = new HashSet<string>();

        foreach (var ev in calendar.Events)
        {
            if (ev.DtStart == null || ev.DtEnd == null) continue;

            var uid = ev.Uid ?? $"{extCal.Name}-{ev.DtStart}-{ev.DtEnd}";
            var newStart = ev.DtStart.AsUtc;
            var newEnd = ev.DtEnd.AsUtc;

            if (local.Any(b => b.StartDate.Date == newStart.Date && b.EndDate.Date == newEnd.Date)) continue;
            keepUids.Add(uid);

            var existing = await _context.ExternalBookings
                .FirstOrDefaultAsync(e => e.ExternalId == uid);

            if (existing == null)
            {
                // Новая бронь
                _context.ExternalBookings.Add(new ExternalBooking
                {
                    ApartmentId = extCal.ApartmentId,
                    ExternalId = uid,
                    Source = extCal.Name,
                    StartDate = newStart,
                    EndDate = newEnd,
                });
                newBookings.Add($"{extCal.Name}: {newStart:dd.MM} — {newEnd:dd.MM}");
            }
            else if (existing.StartDate != newStart || existing.EndDate != newEnd)
            {
                // Даты изменились
                var oldStart = existing.StartDate;
                var oldEnd = existing.EndDate;
                existing.StartDate = newStart;
                existing.EndDate = newEnd;
                changedBookings.Add(
                    $"{extCal.Name}: {oldStart:dd.MM}–{oldEnd:dd.MM} → {newStart:dd.MM}–{newEnd:dd.MM}");
            }
        }

        // Удаляем отменённые
        var uids = calendar.Events.Select(e => e.Uid).ToHashSet();
        var toDelete = await _context.ExternalBookings
            .Where(e => e.ApartmentId == extCal.ApartmentId
                && e.Source == extCal.Name
                && !keepUids.Contains(e.ExternalId))
            .ToListAsync();
        _context.ExternalBookings.RemoveRange(toDelete);

        extCal.LastSync = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // ── Уведомления ─────────────────────────────────────────────────
        var apt = await _context.Apartments.FindAsync(extCal.ApartmentId);
        var aptName = apt?.Title ?? $"Квартира #{extCal.ApartmentId}";
        var tokens = await _context.DeviceTokens.Select(t => t.Token).ToListAsync();

        foreach (var info in newBookings)
        {
            var msg = $"🔔 Новая бронь ({info})\n🏠 {aptName}";

            try { await _max.SendNotificationAsync(msg); } catch { }

            foreach (var token in tokens)
                try { await _fcm.SendAsync(token, "Новая внешняя бронь", $"{aptName}: {info}"); } catch { }
        }

        foreach (var info in changedBookings)
        {
            var msg = $"✏️ Изменение брони ({info})\n🏠 {aptName}";

            try { await _max.SendNotificationAsync(msg); } catch { }

            foreach (var token in tokens)
                try { await _fcm.SendAsync(token, "Изменение брони", $"{aptName}: {info}"); } catch { }
        }
    }
}