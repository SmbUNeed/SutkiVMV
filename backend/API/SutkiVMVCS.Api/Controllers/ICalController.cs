using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.ModelsDTO;
using SutkiVMV.Models;
using SutkiVMV.Services;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/ical")]
public class ICalController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICalService _icalService;

    public ICalController(AppDbContext context, ICalService icalService)
    {
        _context = context;
        _icalService = icalService;
    }

    // Экспорт — публичный, Airbnb/Авито будут опрашивать этот URL
    [HttpGet("{apartmentId:int}/export")]
    public async Task<IActionResult> Export(int apartmentId)
    {
        var ics = await _icalService.ExportAsync(apartmentId);
        return Content(ics, "text/calendar");
    }

    // Список внешних календарей квартиры
    [HttpGet("{apartmentId:int}/external")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetExternal(int apartmentId)
    {
        var cals = await _context.ExternalCalendars
            .Where(c => c.ApartmentId == apartmentId)
            .AsNoTracking()
            .ToListAsync();
        return Ok(cals);
    }

    // Добавить внешний календарь
    [HttpPost("{apartmentId:int}/external")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddExternal(int apartmentId, [FromBody] AddExternalCalendarDto dto, CancellationToken ct)
    {
        var cal = new ExternalCalendar
        {
            ApartmentId = apartmentId,
            Name = dto.Name,
            ICalUrl = dto.ICalUrl
        };
        _context.ExternalCalendars.Add(cal);
        await _context.SaveChangesAsync(ct);
        return Ok(cal);
    }

    // Удалить внешний календарь
    [HttpDelete("external/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteExternal(int id, CancellationToken ct)
    {
        var cal = await _context.ExternalCalendars.FindAsync(id);
        if (cal == null) return NotFound();
        _context.ExternalCalendars.Remove(cal);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // Ручная синхронизация
    [HttpPost("{apartmentId:int}/sync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Sync(int apartmentId, CancellationToken ct)
    {
        var cals = await _context.ExternalCalendars
            .Where(c => c.ApartmentId == apartmentId)
            .ToListAsync(ct);

        foreach (var cal in cals)
            await _icalService.ImportAsync(cal);

        return Ok(new { synced = cals.Count });
    }
}