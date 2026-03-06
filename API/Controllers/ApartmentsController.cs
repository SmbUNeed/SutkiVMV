using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApartmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApartmentsController(AppDbContext context)
    {
        _context = context;
    }

    // GET api/apartments/all — все квартиры (для админки)
    // [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<Apartment>>> GetAll()
        => Ok(await _context.Apartments.ToListAsync());

    // GET api/apartments/active — активные и не удалённые
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Apartment>>> GetActive()
        => Ok(await _context.Apartments.Where(a => a.IsActive && !a.IsDeleted).ToListAsync());

    // GET api/apartments/detailed — с фото, удобствами и бронированиями (для фронта)
    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetDetailed()
    {
        var apartments = await _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Select(a => new ApartmentDto
            {
                Id = a.Id,
                Title = a.Title,
                Address = a.Address,
                Description = a.Description,
                Guests = a.Guests,
                PricePerNight = a.PricePerNight,
                RoomsCount = a.RoomsCount,
                Area = a.Area,
                MainImage = a.ApartmentGallery
                    .Where(g => g.IsMain)
                    .Select(g => g.ImagePath)
                    .FirstOrDefault(),
                Gallery = a.ApartmentGallery
                    .Select(g => g.ImagePath)
                    .ToList(),
                Amenities = a.ApartmentAmenities
                    .Select(aa => aa.Amenitie.Name)
                    .ToList(),
                Bookings = a.Bookings
                    .Select(b => new BookingDto
                    {
                        StartDate = b.StartDate,
                        EndDate = b.EndDate,
                        Status = b.Status
                    }).ToList()
            })
            .ToListAsync();

        return Ok(apartments);
    }

    // GET api/apartments/filter — фильтрация по цене и комнатам
    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<Apartment>>> Filter(
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int? rooms)
    {
        var query = _context.Apartments.Where(a => a.IsActive && !a.IsDeleted);

        if (minPrice.HasValue) query = query.Where(a => a.PricePerNight >= minPrice);
        if (maxPrice.HasValue) query = query.Where(a => a.PricePerNight <= maxPrice);
        if (rooms.HasValue)    query = query.Where(a => a.RoomsCount == rooms);

        return Ok(await query.ToListAsync());
    }

    // POST api/apartments/new — добавить квартиру
    // [Authorize(Roles = "Admin")]
    [HttpPost("new")]
    public async Task<ActionResult<Apartment>> Create(Apartment apartment)
    {
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        return Ok(apartment);
    }

    // PUT api/apartments/{id} — обновить квартиру
    // [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Apartment updated)
    {
        var apartment = await _context.Apartments.FindAsync(id);
        if (apartment == null || apartment.IsDeleted)
            return NotFound(new { message = $"Квартира с id={id} не найдена" });

        apartment.Title        = updated.Title;
        apartment.Address      = updated.Address;
        apartment.Description  = updated.Description;
        apartment.PricePerNight = updated.PricePerNight;
        apartment.RoomsCount   = updated.RoomsCount;
        apartment.Guests       = updated.Guests;
        apartment.Area         = updated.Area;
        apartment.IsActive     = updated.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PATCH api/apartments/restore/{id} — восстановить удалённую квартиру
    // [Authorize(Roles = "Admin")]
    [HttpPatch("restore/{id:int}")]
    public async Task<ActionResult> Restore(int id)
    {
        var apartment = await _context.Apartments.FindAsync(id);
        if (apartment == null)
            return NotFound(new { message = $"Квартира с id={id} не найдена" });

        apartment.IsDeleted = false;
        apartment.IsActive = true;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE api/apartments/{id} — мягкое удаление по id
    // [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var apartment = await _context.Apartments.FindAsync(id);
        if (apartment == null)
            return NotFound(new { message = $"Квартира с id={id} не найдена" });

        apartment.IsDeleted = true;
        apartment.IsActive  = false;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
