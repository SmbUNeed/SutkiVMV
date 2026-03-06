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

    public ApartmentsController(AppDbContext context)
    {
        _context = context;
    }
    #region HttpGet
    //[Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<Apartment>>> GetAllApartments()
    {
        return await _context.Apartments.ToListAsync();
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Apartment>>> GetActiveApartments()
    {
        return await _context.Apartments.Where(a => a.IsActive && !a.IsDeleted).ToListAsync();
    }

    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<Apartment>>> FilterApartments(
    [FromQuery] decimal? minPrice,
    [FromQuery] decimal? maxPrice,
    [FromQuery] int? rooms)
    {
        var query = _context.Apartments.Where(a => !a.IsDeleted && a.IsActive);

        if (minPrice.HasValue) query = query.Where(a => a.PricePerNight >= minPrice);
        if (maxPrice.HasValue) query = query.Where(a => a.PricePerNight <= maxPrice);
        if (rooms.HasValue) query = query.Where(a => a.RoomsCount == rooms);

        return Ok(await query.ToListAsync());
    }

    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetApartmentsWithDetails()
    {
        var apartments = await _context.Apartments
        .Where(a => a.IsActive && !a.IsDeleted)
        .Include(a => a.ApartmentGallery)
        .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenitie)
        .Include(a => a.Bookings)
        .AsNoTracking()
        .Select(a => new ApartmentDto
        {
            Id = a.Id,  
            Title = a.Title,
            Description = a.Description,
            Guests = a.Guests,
            PricePerNight = a.PricePerNight,
            RoomsCount = a.RoomsCount,
            Area = a.Area,
            Gallery = a.ApartmentGallery.Select(ag => ag.ImagePath).ToList(),
            Amenities = a.ApartmentAmenities
                            .Select(aa => aa.Amenitie.Name)
                            .ToList(),
            Bookings = a.Bookings.Select(b => new BookingDto
            {
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Status = b.Status
            }).ToList()
        })
        .ToListAsync();

        return Ok(apartments);
    }
    #endregion

    #region HttpPost
    //[Authorize(Roles = "Admin")]
    [HttpPost("new")]
    public async Task<ActionResult<Apartment>> PostApartment(Apartment apartment)
    {
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        return Ok(apartment);
    }
    #endregion

    #region HttpPatch
    //[Authorize(Roles = "Admin")]
    [HttpPatch("restore-by-id/{id}")]
    public async Task<ActionResult> RestoreApartment(int id)
    {
        Apartment? apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null) return NotFound(new { message = $"Квартира с id={id} не найден(а)" });

        apartment.IsDeleted = false;

        await _context.SaveChangesAsync();

        return NoContent();
    }
    #endregion

    #region HttpPut
    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateApartment(int id, Apartment updated)
    {
        var apartment = await _context.Apartments.FindAsync(id);
        if (apartment == null || apartment.IsDeleted)
            return NotFound();

        apartment.Title = updated.Title;
        apartment.Address = updated.Address;
        apartment.Description = updated.Description;
        apartment.PricePerNight = updated.PricePerNight;
        apartment.RoomsCount = updated.RoomsCount;
        apartment.Area = updated.Area;
        apartment.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }
    #endregion

    #region HttpDelete
    //[Authorize(Roles = "Admin")]
    [HttpDelete("by-id/{id:int}")]
    public async Task<ActionResult> DeleteApartmentById(int id)
    {
        Apartment? apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null) return NotFound(new { message = $"Квартира с id={id} не найден(а)" });

        apartment.IsDeleted = true;
        apartment.IsActive = false;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    //[Authorize(Roles = "Admin")]
    [HttpDelete("by_title/{title}")]
    public async Task<ActionResult> CanselApartmentDeletionByTitle(string title)
    {
        Apartment? apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.Title.Replace(" ", "").ToLower() == title.Replace(" ", "").ToLower());

        if (apartment == null) return NotFound(new { message = $"Квартира {title} не найден(а)" });

        apartment.IsDeleted = true;

        await _context.SaveChangesAsync();

        return NoContent();
    }
    #endregion
}