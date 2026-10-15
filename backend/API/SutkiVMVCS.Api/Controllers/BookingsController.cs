using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Services.Shared;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _context;
    public BookingsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/bookings — для административной панели
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminBookingDto>>> GetAll()
    {
        var bookings = await _context.Bookings
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Ok(bookings.Select(ToAdminDto));
    }

    // GET /api/bookings/apartment/{apartmentId}
    [HttpGet("apartment/{apartmentId:int}")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetByApartment(int apartmentId)
    {
        var bookings = await _context.Bookings
            .Where(b => b.ApartmentId == apartmentId)
            .OrderBy(b => b.StartDate)
            .AsNoTracking()
            .ToListAsync();

        return Ok(bookings.Select(b => new BookingDto
        {
            Id = b.Id,
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            Status = b.Status
        }));
    }

    // GET /api/bookings/client/{clientId}
    [HttpGet("client/{clientId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminBookingDto>>> GetByClient(int clientId)
    {
        var bookings = await _context.Bookings
            .Where(b => b.ClientId == clientId)
            .Include(b => b.Apartment)
            .Include(b => b.Client)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Ok(bookings.Select(ToAdminDto));
    }

    // POST /api/bookings — создание брони администратором
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminBookingDto>> Create(CreateBookingDto dto, BookingService bookingService, CancellationToken ct)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        var apartment = await _context.Apartments.FindAsync(dto.ApartmentId, ct);
        if (apartment == null || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена" });

        var client = await _context.Clients.FindAsync(dto.ClientId, ct);
        if (client == null)
            return NotFound(new { message = "Клиент не найден" });

        var (booking, error) = await bookingService.CreateAsync(
            apartment, dto.ClientId, dto.Guests, dto.StartDate, dto.EndDate, "Вручную", ct, "Confirmed");
        if (booking == null) return Conflict(new { message = error });

        return Ok();
    }

    // PATCH /api/bookings/{id}/status — изменить статус (confirm / cancel)
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDto dto, CancellationToken ct)
    {
        var booking = await _context.Bookings.FindAsync(id, ct);
        if (booking == null) return NotFound();

        var allowed = new[] { "Pending", "Confirmed", "Cancelled" };
        if (!allowed.Contains(dto.Status))
            return BadRequest(new { message = $"Недопустимый статус. Допустимы: {string.Join(", ", allowed)}" });

        booking.Status = dto.Status;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // PATCH /api/bookings/{id}/cancel — быстрая отмена (обратная совместимость)
    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Cancel(int id, CancellationToken ct)
    {
        var booking = await _context.Bookings.FindAsync(id, ct);
        if (booking == null) return NotFound();
        if (booking.Status == "Cancelled")
            return BadRequest(new { message = "Бронирование уже отменено" });

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/confirm")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Confirm(int id, CancellationToken ct)
    {
        var booking = await _context.Bookings.FindAsync(id, ct);
        if (booking == null) return NotFound();
        if (booking.Status == "Confirmed")
            return BadRequest(new { message = "Бронирование уже подтверждено" });

        booking.Status = "Confirmed";
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, [FromBody] CreateBookingDto dto, CancellationToken ct)
    {
        var booking = await _context.Bookings.FindAsync(id, ct);
        if (booking == null) return NotFound();

        booking.ApartmentId = dto.ApartmentId;
        booking.ClientId = dto.ClientId;
        booking.StartDate = dto.StartDate;
        booking.EndDate = dto.EndDate;
        booking.Guests = dto.Guests;

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var booking = await _context.Bookings.FindAsync(id, ct);
        if (booking == null) return NotFound();
        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AdminBookingDto ToAdminDto(Booking b) => new()
    {
        Id = b.Id,
        ApartmentId = b.ApartmentId,
        ApartmentTitle = b.Apartment?.Title ?? "",
        ClientId = b.ClientId,
        ClientTag = b.Client?.AdminTag ?? "",
        ClientName = b.Client?.FullName ?? "",
        ClientPhone = b.Client?.PhoneNumber ?? "",
        StartDate = b.StartDate,
        EndDate = b.EndDate,
        TotalPrice = b.TotalPrice,
        Status = b.Status,
        Source = b.Source,
        CreatedAt = b.CreatedAt,
        Guests = b.Guests
    };
}