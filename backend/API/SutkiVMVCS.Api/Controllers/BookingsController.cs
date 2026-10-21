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

        var apartment = await _context.Apartments.FindAsync(new object[] { dto.ApartmentId }, ct);
        if (apartment == null || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена" });

        var client = await _context.Clients.FindAsync(new object[] { dto.ClientId }, ct);
        if (client == null)
            return NotFound(new { message = "Клиент не найден" });

        var (booking, error) = await bookingService.CreateAsync(
            apartment, dto.ClientId, dto.Guests, dto.StartDate, dto.EndDate, "Вручную", ct, "Confirmed", price: dto.AdminPrice);
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
        var booking = await _context.Bookings.FindAsync(new object[] { id }, ct);
        if (booking == null) return NotFound();

        var apartment = await _context.Apartments.FindAsync(new object[] { dto.ApartmentId }, ct);
        if (apartment == null) return BadRequest("Квартира не найдена");

        var s = dto.StartDate.ToUniversalTime();
        var e = dto.EndDate.ToUniversalTime();

        if (dto.StartDate >= dto.EndDate) return BadRequest("Дата заезда должна быть раньше даты выезда");

        bool busy =
            await _context.Bookings.AnyAsync(b =>
                b.Id != id && b.ApartmentId == dto.ApartmentId && b.Status != "Cancelled" &&
                b.StartDate < e && b.EndDate > s, ct) ||
            await _context.ExternalBookings.AnyAsync(x =>
                x.ApartmentId == dto.ApartmentId && x.StartDate < e && x.EndDate > s, ct);
        if (busy) return Conflict("Квартира занята в выбранные даты");

        var rules = await _context.PriceRules.ToListAsync(ct);
        var basePrice = PricingService.CalculateTotal(s, e, dto.Guests, apartment, rules);
        var total = dto.AdminPrice ?? (booking.DiscountAmount > 0 ? Math.Max(basePrice - booking.DiscountAmount, 0) : basePrice);

        if (total < 0 || total > basePrice) return BadRequest("Некорректная цена");
        if (booking.PaidAmount > 0 && total != booking.TotalPrice) return BadRequest("Бронь уже оплачена");

        booking.ApartmentId = dto.ApartmentId;
        booking.ClientId = dto.ClientId;
        booking.StartDate = s;
        booking.EndDate = e;
        booking.Guests = dto.Guests;
        booking.BasePrice = basePrice;
        booking.TotalPrice = total;
        booking.DiscountAmount = basePrice - total;
        booking.PrepaymentAmount = PricingService.CalculatePrepayment(s, dto.Guests, apartment, rules, basePrice, total);
        if (booking.DiscountAmount > 0 && booking.Status == "Pending") booking.Status = "AwaitingPayment";

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
        BasePrice = b.BasePrice,
        DiscountAmount = b.DiscountAmount,
        PrepaymentAmount = b.PrepaymentAmount,
        PaidAmount = b.PaidAmount,
        Status = b.Status,
        Source = b.Source,
        CreatedAt = b.CreatedAt,
        Guests = b.Guests
    };
}