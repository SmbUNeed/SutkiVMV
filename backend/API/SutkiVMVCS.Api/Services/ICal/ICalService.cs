using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using SutkiVMV.Data;
using SutkiVMV.Models;
using Microsoft.EntityFrameworkCore;

namespace SutkiVMV.Services;

public class ICalService
{
    private readonly AppDbContext _context;

    public ICalService(AppDbContext context)
    {
        _context = context;
    }

    // Экспорт броней квартиры в .ics
    public async Task<string> ExportAsync(int apartmentId)
    {
        var apt = await _context.Apartments
            .Include(a => a.Bookings)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == apartmentId);

        if (apt == null) return "";

        var calendar = new Calendar();

        // Свои брони
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

        // Внешние брони
        var external = await _context.ExternalBookings
            .Where(e => e.ApartmentId == apartmentId)
            .AsNoTracking()
            .ToListAsync();

        foreach (var e in external)
        {
            calendar.Events.Add(new CalendarEvent
            {
                Uid = e.ExternalId,
                Summary = $"Занято ({e.Source})",
                Start = new CalDateTime(e.StartDate, "UTC"),
                End = new CalDateTime(e.EndDate, "UTC"),
            });
        }

        return new CalendarSerializer().SerializeToString(calendar);
    }

    // Импорт броней из внешнего ics URL
    public async Task ImportAsync(ExternalCalendar extCal)
    {
        using var http = new HttpClient();
        string icsContent;
        try { icsContent = await http.GetStringAsync(extCal.ICalUrl); }
        catch { return; }

        var calendar = Calendar.Load(icsContent);

        foreach (var ev in calendar.Events)
        {
            var uid = ev.Uid ?? $"{extCal.Name}-{ev.DtStart}-{ev.DtEnd}";

            var exists = await _context.ExternalBookings
                .AnyAsync(e => e.ExternalId == uid);

            if (!exists)
            {
                _context.ExternalBookings.Add(new ExternalBooking
                {
                    ApartmentId = extCal.ApartmentId,
                    ExternalId = uid,
                    Source = extCal.Name,
                    StartDate = ev.DtStart.AsUtc,
                    EndDate = ev.DtEnd.AsUtc,
                });
            }
        }

        // Удалить брони которых больше нет в календаре
        var uids = calendar.Events.Select(e => e.Uid).ToHashSet();
        var toDelete = await _context.ExternalBookings
            .Where(e => e.ApartmentId == extCal.ApartmentId
                     && e.Source == extCal.Name
                     && !uids.Contains(e.ExternalId))
            .ToListAsync();
        _context.ExternalBookings.RemoveRange(toDelete);

        extCal.LastSync = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}