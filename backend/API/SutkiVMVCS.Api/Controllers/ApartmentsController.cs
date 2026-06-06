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

    // ═══════════════════════════════════════════════════════════════════
    //  КВАРТИРЫ
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApartmentDto>> GetById(int id)
    {
        var apt = await _context.Apartments
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Bookings)
            .Include(a => a.Rules).ThenInclude(ar => ar.Rule)
            .Include(a => a.Nearby).ThenInclude(an => an.NearbyPlace)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        return apt == null ? NotFound() : Ok(ToDto(apt));
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetAll()
    {
        var apts = await _context.Apartments
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Rules).ThenInclude(ar => ar.Rule)
            .Include(a => a.Nearby).ThenInclude(an => an.NearbyPlace)
            .AsNoTracking()
            .ToListAsync();
        return Ok(apts.Select(ToDto));
    }

    [HttpGet("short")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ApartmentShortDto>>> GetShort()
    {
        var apts = await _context.Apartments
            .Select(a => new ApartmentShortDto { Id = a.Id, Title = a.Title })
            .ToListAsync();
        return Ok(apts);
    }

    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> GetDetailed()
    {
        var apts = await _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Bookings)
            .Include(a => a.Rules).ThenInclude(ar => ar.Rule)
            .Include(a => a.Nearby).ThenInclude(an => an.NearbyPlace)
            .AsNoTracking()
            .ToListAsync();
        return Ok(apts.Select(ToDto));
    }

    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<ApartmentDto>>> Filter(
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] int? rooms)
    {
        var query = _context.Apartments
            .Where(a => a.IsActive && !a.IsDeleted)
            .Include(a => a.ApartmentGallery)
            .Include(a => a.ApartmentAmenities).ThenInclude(aa => aa.Amenity)
            .Include(a => a.Rules).ThenInclude(ar => ar.Rule)
            .Include(a => a.Nearby).ThenInclude(an => an.NearbyPlace)
            .AsNoTracking().AsQueryable();

        if (minPrice.HasValue) query = query.Where(a => a.PricePerNight >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(a => a.PricePerNight <= maxPrice.Value);
        if (rooms.HasValue) query = query.Where(a => a.RoomsCount == rooms.Value);

        return Ok((await query.ToListAsync()).Select(ToDto));
    }

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

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, Apartment updated, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id, ct);
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

    [HttpPost("{id:int}/amenities")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetAmenities(int id, SetAmenitiesDto dto, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id, ct);
        if (apt == null) return NotFound();

        var old = await _context.ApartmentAmenities.Where(aa => aa.ApartmentId == id).ToListAsync(ct);
        _context.ApartmentAmenities.RemoveRange(old);

        foreach (var amenityId in dto.AmenityIds.Distinct())
            if (await _context.Amenities.AnyAsync(a => a.Id == amenityId, ct))
                _context.ApartmentAmenities.Add(new ApartmentAmenity { ApartmentId = id, AmenityId = amenityId });

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/rules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetRules(int id, [FromBody] SetIdsDto dto, CancellationToken ct)
    {
        if (!await _context.Apartments.AnyAsync(a => a.Id == id && !a.IsDeleted, ct))
            return NotFound();

        var old = await _context.ApartmentRules.Where(ar => ar.ApartmentId == id).ToListAsync(ct);
        _context.ApartmentRules.RemoveRange(old);

        foreach (var ruleId in dto.Ids.Distinct())
            if (await _context.Rules.AnyAsync(r => r.Id == ruleId, ct))
                _context.ApartmentRules.Add(new ApartmentRule { ApartmentId = id, RuleId = ruleId });

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/nearby")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddNearbyToApartment(int id, [FromBody] AddNearbyToApartmentDto dto, CancellationToken ct)
    {
        var exists = await _context.Set<ApartmentNearbyPlace>()
            .AnyAsync(x => x.ApartmentId == id && x.NearbyPlaceId == dto.NearbyPlaceId, ct);
        if (exists) return Conflict("Место уже добавлено");

        _context.Set<ApartmentNearbyPlace>().Add(new ApartmentNearbyPlace
        {
            ApartmentId = id,
            NearbyPlaceId = dto.NearbyPlaceId,
            Distance = dto.Distance
        });
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/rules/{ruleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRule(int id, int ruleId, CancellationToken ct)
    {
        var link = await _context.ApartmentRules
            .FirstOrDefaultAsync(x => x.ApartmentId == id && x.RuleId == ruleId, ct);
        if (link == null) return NotFound();
        _context.ApartmentRules.Remove(link);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/nearby/{nearbyPlaceId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveNearby(int id, int nearbyPlaceId, CancellationToken ct)
    {
        var link = await _context.Set<ApartmentNearbyPlace>()
            .FirstOrDefaultAsync(x => x.ApartmentId == id && x.NearbyPlaceId == nearbyPlaceId, ct);
        if (link == null) return NotFound();
        _context.Set<ApartmentNearbyPlace>().Remove(link);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("restore/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Restore(int id, CancellationToken ct)
    {
        var apt = await _context.Apartments.FindAsync(id, ct);
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
        var apt = await _context.Apartments.FindAsync(id, ct);
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
        var apt = await _context.Apartments.FindAsync(id, ct);
        if (apt == null) return NotFound();
        apt.IsActive = isActive;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ГЛОБАЛЬНЫЕ ПРАВИЛА
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("rules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<RuleDto>>> GetAllRules()
    {
        var rules = await _context.Rules.AsNoTracking().ToListAsync();
        return Ok(rules.Select(r => new RuleDto { Id = r.Id, Icon = r.Icon, Text = r.Text }));
    }

    [HttpPost("rules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RuleDto>> CreateRule([FromBody] CreateRuleDto dto, CancellationToken ct)
    {
        var rule = new Rule { Icon = dto.Icon, Text = dto.Text };
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return Ok(new RuleDto { Id = rule.Id, Icon = rule.Icon, Text = rule.Text });
    }

    [HttpDelete("rules/{ruleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteRule(int ruleId, CancellationToken ct)
    {
        var rule = await _context.Rules.FindAsync(ruleId, ct);
        if (rule == null) return NotFound();
        _context.Rules.Remove(rule);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("rules/{ruleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateRule(int ruleId, CreateRuleDto dto, CancellationToken ct)
    {
        var rule = await _context.Rules.FindAsync(ruleId, ct);
        if (rule == null) return NotFound();
        rule.Text = dto.Text;
        rule.Icon = dto.Icon;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ГЛОБАЛЬНЫЕ МЕСТА РЯДОМ
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("nearby-places")]
    [Authorize (Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<NearbyPlaceDto>>> GetAllNearbyPlaces()
    {
        var nearby = await _context.NearbyPlaces.AsNoTracking().ToListAsync();
        return Ok(nearby.Select(n => new NearbyPlaceDto { Id = n.Id, Icon = n.Icon, Name = n.Name }));
    }

    [HttpPost("nearby-places")]
    [Authorize (Roles = "Admin")]
    public async Task<ActionResult<NearbyPlaceDto>> CreateNearbyPlace([FromBody] CreateNearbyDto dto, CancellationToken ct)
    {
        var place = new NearbyPlace { Icon = dto.Icon, Name = dto.Name };
        _context.NearbyPlaces.Add(place);
        await _context.SaveChangesAsync(ct);
        return Ok(new NearbyPlaceDto { Id = place.Id, Icon = place.Icon, Name = place.Name });
    }

    [HttpDelete("nearby-places/{nearbyId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteNearbyPlace(int nearbyId, CancellationToken ct)
    {
        var place = await _context.NearbyPlaces.FindAsync(nearbyId, ct);
        if (place == null) return NotFound();
        _context.NearbyPlaces.Remove(place);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("nearby-places/{nearbyId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateNearbyPlace(int nearbyId, CreateNearbyDto dto, CancellationToken ct)
    {
        var place = await _context.NearbyPlaces.FindAsync(nearbyId, ct);
        if (place == null) return NotFound();
        place.Name = dto.Name;
        place.Icon = dto.Icon;
        place.WikiPlaceId = dto.WikiPlaceId;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ToDto
    // ═══════════════════════════════════════════════════════════════════

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
                     .Select(ar => new RuleDto { Id = ar.Rule.Id, Icon = ar.Rule.Icon, Text = ar.Rule.Text })
                     .ToList(),
        Nearby = a.Nearby
                     .Select(an => new NearbyPlaceDto {
                         Id = an.NearbyPlace.Id,
                         Icon = an.NearbyPlace.Icon,
                         Name = an.NearbyPlace.Name,
                         Distance = an.Distance,
                         WikiPlaceId = an.NearbyPlace.WikiPlaceId
                     }).ToList()
                     };
}