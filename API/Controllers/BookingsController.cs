using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

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

    // GET api/bookings — все бронирования (для админки)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Booking>>> GetAll()
        => Ok(await _context.Bookings.OrderByDescending(b => b.StartDate).ToListAsync());

    // GET api/bookings/apartment/{apartmentId} — бронирования по квартире
    [HttpGet("apartment/{apartmentId:int}")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetByApartment(int apartmentId)
        => Ok(await _context.Bookings
            .Where(b => b.ApartmentId == apartmentId)
            .OrderBy(b => b.StartDate)
            .ToListAsync());

    // GET api/bookings/client/{clientId} — бронирования по клиенту
    [HttpGet("client/{clientId:int}")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetByClient(int clientId)
        => Ok(await _context.Bookings
            .Where(b => b.ClientId == clientId)
            .ToListAsync());

    // POST api/bookings — создать бронирование
    // [Authorize(Roles = "User")]
    [HttpPost]
    public async Task<ActionResult<Booking>> Create(CreateBookingDto dto)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        var apartment = await _context.Apartments.FindAsync(dto.ApartmentId);
        if (apartment == null || !apartment.IsActive || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена или недоступна" });

        var client = await _context.Clients.FindAsync(dto.ClientId);
        if (client == null)
            return NotFound(new { message = "Клиент не найден" });

        bool overlap = await _context.Bookings.AnyAsync(b =>
            b.ApartmentId == dto.ApartmentId &&
            b.Status != "Cancelled" &&
            b.StartDate < dto.EndDate &&
            b.EndDate > dto.StartDate);

        if (overlap)
            return Conflict(new { message = "Квартира занята в выбранные даты" });

        var nights = (dto.EndDate - dto.StartDate).Days;
        var booking = new Booking
        {
            ApartmentId = dto.ApartmentId,
            ClientId    = dto.ClientId,
            StartDate   = dto.StartDate,
            EndDate     = dto.EndDate,
            TotalPrice  = apartment.PricePerNight * nights,
            Status      = "Pending",
            CreatedAt   = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return Ok(booking);
    }

    // PATCH api/bookings/{id}/cancel — отменить бронирование
    [HttpPatch("{id:int}/cancel")]
    public async Task<ActionResult> Cancel(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound(new { message = $"Бронирование с id={id} не найдено" });

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
