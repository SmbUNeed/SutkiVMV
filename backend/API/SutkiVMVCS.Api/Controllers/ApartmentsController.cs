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
            .Include(a => a.Rules.OrderBy(r => r.Order))
            .Include(a => a.Nearby)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        return apt == null ? NotFound() : Ok(ToDto(apt));
    }

    // GET /api/apartments/all
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetAll()
    {
        var apts = await _context.Apartments
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Rules.OrderBy(r => r.Order))
            .Include(a => a.Nearby)
            .AsNoTracking()
            .ToListAsync();
        return Ok(apts.Select(ToDto));
    }

    // GET /api/apartments/detailed
    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetDetailed()
    {
        var apts = await _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Bookings)
            .Include(a => a.Rules.OrderBy(r => r.Order))
            .Include(a => a.Nearby)
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
            .Include(a => a.Rules.OrderBy(r => r.Order))
            .Include(a => a.Nearby)
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

    // POST /api/apartments/{id}/amenities
    [HttpPost("{id:int}/amenities")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetAmenities(int id, SetAmenitiesDto dto, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null) return NotFound();

        var old = await _context.ApartmentAmenities.Where(aa => aa.ApartmentId == id).ToListAsync();
        _context.ApartmentAmenities.RemoveRange(old);

        foreach (var amenityId in dto.AmenityIds.Distinct())
            if (await _context.Amenities.AnyAsync(a => a.Id == amenityId))
                _context.ApartmentAmenities.Add(new ApartmentAmenity { ApartmentId = id, AmenityId = amenityId });

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── RULES ───────────────────────────────────────────────────────────

    // GET /api/apartments/{id}/rules
    [HttpGet("{id:int}/rules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentRuleDto>>> GetRules(int id)
    {
        var rules = await _context.ApartmentRules
            .Where(r => r.ApartmentId == id)
            .OrderBy(r => r.Order)
            .AsNoTracking()
            .ToListAsync();
        return Ok(rules.Select(r => new ApartmentRuleDto { Id = r.Id, Icon = r.Icon, Text = r.Text, Order = r.Order }));
    }

    // POST /api/apartments/{id}/rules
    [HttpPost("{id:int}/rules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AddRule(int id, CreateRuleDto dto, CancellationToken ct)
    {
        if (!await _context.Apartments.AnyAsync(a => a.Id == id && !a.IsDeleted, ct))
            return NotFound();

        _context.ApartmentRules.Add(new ApartmentRule
        {
            ApartmentId = id,
            Icon = dto.Icon,
            Text = dto.Text,
            Order = dto.Order
        });
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/apartments/{id}/rules/{ruleId}
    [HttpDelete("{id:int}/rules/{ruleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteRule(int id, int ruleId, CancellationToken ct)
    {
        var rule = await _context.ApartmentRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ApartmentId == id, ct);
        if (rule == null) return NotFound();
        _context.ApartmentRules.Remove(rule);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // PUT /api/apartments/{id}/rules/{ruleId}
    [HttpPut("{id:int}/rules/{ruleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateRule(int id, int ruleId, CreateRuleDto dto, CancellationToken ct)
    {
        var rule = await _context.ApartmentRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ApartmentId == id, ct);
        if (rule == null) return NotFound();
        rule.Icon = dto.Icon;
        rule.Text = dto.Text;
        rule.Order = dto.Order;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── NEARBY ──────────────────────────────────────────────────────────

    // GET /api/apartments/{id}/nearby
    [HttpGet("{id:int}/nearby")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentNearbyDto>>> GetNearby(int id)
    {
        var nearby = await _context.ApartmentNearby
            .Where(n => n.ApartmentId == id)
            .AsNoTracking()
            .ToListAsync();
        return Ok(nearby.Select(n => new ApartmentNearbyDto { Id = n.Id, Icon = n.Icon, Name = n.Name, Distance = n.Distance }));
    }

    // POST /api/apartments/{id}/nearby
    [HttpPost("{id:int}/nearby")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AddNearby(int id, CreateNearbyDto dto, CancellationToken ct)
    {
        if (!await _context.Apartments.AnyAsync(a => a.Id == id && !a.IsDeleted, ct))
            return NotFound();

        _context.ApartmentNearby.Add(new ApartmentNearby
        {
            ApartmentId = id,
            Icon = dto.Icon,
            Name = dto.Name,
            Distance = dto.Distance
        });
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/apartments/{id}/nearby/{nearbyId}
    [HttpDelete("{id:int}/nearby/{nearbyId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteNearby(int id, int nearbyId, CancellationToken ct)
    {
        var nearby = await _context.ApartmentNearby.FirstOrDefaultAsync(n => n.Id == nearbyId && n.ApartmentId == id, ct);
        if (nearby == null) return NotFound();
        _context.ApartmentNearby.Remove(nearby);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // PUT /api/apartments/{id}/nearby/{nearbyId}
    [HttpPut("{id:int}/nearby/{nearbyId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateNearby(int id, int nearbyId, CreateNearbyDto dto, CancellationToken ct)
    {
        var nearby = await _context.ApartmentNearby.FirstOrDefaultAsync(n => n.Id == nearbyId && n.ApartmentId == id, ct);
        if (nearby == null) return NotFound();
        nearby.Icon = dto.Icon;
        nearby.Name = dto.Name;
        nearby.Distance = dto.Distance;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Restore / Delete / Active ────────────────────────────────────────

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

    [HttpPost("{id}/set-active-{isActive:bool}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ChangeActivity(int id, bool isActive, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id);
        if (apt == null) return NotFound();
        apt.IsActive = isActive;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── ToDto ────────────────────────────────────────────────────────────

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
                     .ToList(),
        Rules = a.Rules
                     .OrderBy(r => r.Order)
                     .Select(r => new ApartmentRuleDto { Id = r.Id, Icon = r.Icon, Text = r.Text, Order = r.Order })
                     .ToList(),
        Nearby = a.Nearby
                     .Select(n => new ApartmentNearbyDto { Id = n.Id, Icon = n.Icon, Name = n.Name, Distance = n.Distance })
                     .ToList()
    };
}