using Microsoft.AspNetCore.Authorization;
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
    public ApartmentsController(AppDbContext context) => _context = context;

    // GET /api/apartments/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApartmentDto>> GetById(int id)
    {
        var apt = await _context.Apartments
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Bookings)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted); // ← добавить

        return apt == null ? NotFound() : Ok(ToDto(apt));
    }

    // GET /api/apartments/all  (все, включая удалённые — для админки)
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetAll()
    {
        var apts = await _context.Apartments
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .AsNoTracking()
            .ToListAsync();
        return Ok(apts.Select(ToDto));
    }

    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetDetailed()
    {
        var apts = await _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Bookings)
            .AsNoTracking()
            .ToListAsync();
        return Ok(apts.Select(ToDto));
    }

    // GET /api/apartments/filter
    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> Filter(
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] int? rooms)
    {
        var query = _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .AsNoTracking().AsQueryable();

        if (minPrice.HasValue) query = query.Where(a => a.PricePerNight >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.PricePerNight <= maxPrice.Value);
        if (rooms.HasValue) query = query.Where(a => a.RoomsCount == rooms.Value);

        return Ok((await query.ToListAsync()).Select(ToDto));
    }

    // POST /api/apartments/new
    [HttpPost("new")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApartmentDto>> Create(CreateApartmentDto dto, CancellationToken ct)
    {
        var apartment = new Apartment
        {
            Title = dto.Title,
            Address = dto.Address,
            Description = dto.Description,
            Guests = dto.Guests,
            PricePerNight = dto.PricePerNight,
            RoomsCount = dto.RoomsCount,
            Area = dto.Area,
            IsActive = dto.IsActive
            // Id, IsDeleted — не трогаем, БД сама
        };
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(apartment));
    }

    // PUT /api/apartments/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, Apartment updated, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null || apt.IsDeleted) return NotFound();

        apt.Title = updated.Title;
        apt.Address = updated.Address;
        apt.Description = updated.Description;
        apt.PricePerNight = updated.PricePerNight;
        apt.RoomsCount = updated.RoomsCount;
        apt.Guests = updated.Guests;
        apt.Area = updated.Area;
        apt.IsActive = updated.IsActive;

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // POST /api/apartments/{id}/amenities  — задать список удобств (полная замена)
    [HttpPost("{id:int}/amenities")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetAmenities(int id, SetAmenitiesDto dto, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null) return NotFound();

        // Удаляем старые связи
        var old = await _context.ApartmentAmenities
            .Where(aa => aa.ApartmentId == id)
            .ToListAsync();
        _context.ApartmentAmenities.RemoveRange(old);

        // Добавляем новые
        foreach (var amenityId in dto.AmenityIds.Distinct())
        {
            if (await _context.Amenities.AnyAsync(a => a.Id == amenityId))
                _context.ApartmentAmenities.Add(new ApartmentAmenity
                {
                    ApartmentId = id,
                    AmenityId = amenityId
                });
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // PATCH /api/apartments/restore/{id}
    [HttpPatch("restore/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Restore(int id, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null) return NotFound();
        apt.IsDeleted = false;
        apt.IsActive = true;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/apartments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null) return NotFound();
        apt.IsDeleted = true;
        apt.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helper ──────────────────────────────────────────────────────────
    internal static ApartmentDto ToDto(Apartment a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Address = a.Address,
        Description = a.Description,
        Guests = a.Guests,
        PricePerNight = a.PricePerNight,
        RoomsCount = a.RoomsCount,
        Area = a.Area,
        IsActive = a.IsActive,
        IsDeleted = a.IsDeleted,
        MainImage = a.ApartmentGallery.FirstOrDefault(g => g.IsMain)?.ImagePath
                     ?? a.ApartmentGallery.FirstOrDefault()?.ImagePath,
        Gallery = a.ApartmentGallery
                     .OrderByDescending(g => g.IsMain)
                     .Select(g => new GalleryItemDto { Id = g.Id, ImagePath = g.ImagePath, IsMain = g.IsMain })
                     .ToList(),
        Amenities = a.ApartmentAmenities
                     .Select(aa => new AmenityDto { Id = aa.Amenity.Id, Name = aa.Amenity.Name, Icon = aa.Amenity.Icon })
                     .ToList(),
        Bookings = a.Bookings
                     .Select(b => new BookingDto { StartDate = b.StartDate, EndDate = b.EndDate, Status = b.Status })
                     .ToList()
    };
}