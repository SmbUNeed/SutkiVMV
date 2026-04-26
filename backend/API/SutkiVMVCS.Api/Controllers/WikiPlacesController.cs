using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/wiki-places")]
public class WikiPlacesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly string _uploadRoot;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public WikiPlacesController(AppDbContext context, UploadSettings upload)
    {
        _context = context;
        _uploadRoot = upload.WikiUploadPath;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GET ALL
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WikiPlaceDto>>> GetAll()
    {
        var places = await _context.WikiPlaces
            .AsNoTracking()
            .ToListAsync();

        return Ok(places.Select(ToDto));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GET BY ID + квартиры рядом
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WikiPlaceDto>> GetById(int id)
    {
        var place = await _context.WikiPlaces
            .Include(w => w.NearbyPlace)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (place == null) return NotFound();

        var dto = ToDto(place);

        if (place.NearbyPlace != null)
        {
            dto.NearbyApartments = await _context.Set<ApartmentNearbyPlace>()
                .Where(an => an.NearbyPlaceId == place.NearbyPlace.Id
                          && an.Apartment.IsActive
                          && !an.Apartment.IsDeleted)
                .Include(an => an.Apartment).ThenInclude(a => a.ApartmentGallery)
                .AsNoTracking()
                .Select(an => new ApartmentDto
                {
                    Id = an.Apartment.Id,
                    Title = an.Apartment.Title,
                    PricePerNight = an.Apartment.PricePerNight,
                    MainImage = an.Apartment.ApartmentGallery
                                       .Where(g => g.IsMain).Select(g => g.ImagePath)
                                       .FirstOrDefault()
                                  ?? an.Apartment.ApartmentGallery
                                       .Select(g => g.ImagePath).FirstOrDefault()
                })
                .ToListAsync();
        }

        return Ok(dto);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<WikiPlaceDto>> Create(CreateWikiPlaceDto dto, CancellationToken ct)
    {
        var wikiPlace = new WikiPlace
        {
            Name = dto.Name,
            Description = dto.Description,
            PhotoPath = dto.PhotoPath,
            Type = Enum.TryParse<WikiPlaceType>(dto.Type, true, out var t) ? t : WikiPlaceType.Other,
            NearbyPlaceId = dto.NearbyPlaceId
        };
        _context.WikiPlaces.Add(wikiPlace);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(wikiPlace));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════════════════

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, UpdateWikiPlaceDto dto, CancellationToken ct)
    {
        var place = await _context.WikiPlaces.FindAsync(id);
        if (place == null) return NotFound();

        place.Name = dto.Name;
        place.Description = dto.Description;
        place.PhotoPath = dto.PhotoPath;
        place.Type = Enum.TryParse<WikiPlaceType>(dto.Type, true, out var t) ? t : WikiPlaceType.Other;
        place.NearbyPlaceId = dto.NearbyPlaceId;

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DELETE
    // ═══════════════════════════════════════════════════════════════════

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var place = await _context.WikiPlaces.FindAsync(id);
        if (place == null) return NotFound();

        // Удаляем фото с диска если есть
        if (!string.IsNullOrEmpty(place.PhotoPath))
        {
            var relPath = place.PhotoPath.TrimStart('/').Substring("uploads/wiki/".Length);
            var diskPath = Path.Combine(_uploadRoot, relPath);
            if (System.IO.File.Exists(diskPath)) System.IO.File.Delete(diskPath);
        }

        _context.WikiPlaces.Remove(place);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UPLOAD PHOTO
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost("{id:int}/photo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UploadPhoto(int id, CancellationToken ct,
        [FromForm] IFormFile file)
    {
        var place = await _context.WikiPlaces.FindAsync(id);
        if (place == null) return NotFound(new { message = "Место не найдено" });
        if (file == null || file.Length == 0) return BadRequest(new { message = "Файл не передан" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Недопустимый формат файла" });

        // Удаляем старое фото если есть
        if (!string.IsNullOrEmpty(place.PhotoPath))
        {
            var relPath = place.PhotoPath.TrimStart('/').Substring("uploads/wiki/".Length);
            var diskPath = Path.Combine(_uploadRoot, relPath);
            if (System.IO.File.Exists(diskPath)) System.IO.File.Delete(diskPath);
        }

        // Сохраняем новое фото в /uploads/wiki/{id}/
        var dir = Path.Combine(_uploadRoot, id.ToString());
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(dir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        place.PhotoPath = $"/uploads/wiki/{id}/{fileName}";
        await _context.SaveChangesAsync(ct);

        return Ok(new { photoPath = place.PhotoPath });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DELETE PHOTO
    // ═══════════════════════════════════════════════════════════════════

    [HttpDelete("{id:int}/photo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeletePhoto(int id, CancellationToken ct)
    {
        var place = await _context.WikiPlaces.FindAsync(id);
        if (place == null) return NotFound();
        if (string.IsNullOrEmpty(place.PhotoPath)) return NoContent();

        var relPath = place.PhotoPath.TrimStart('/').Substring("uploads/wiki/".Length);
        var diskPath = Path.Combine(_uploadRoot, relPath);
        if (System.IO.File.Exists(diskPath)) System.IO.File.Delete(diskPath);

        place.PhotoPath = "";
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ToDto
    // ═══════════════════════════════════════════════════════════════════

    static WikiPlaceDto ToDto(WikiPlace wp) => new()
    {
        Id = wp.Id,
        Name = wp.Name,
        Description = wp.Description,
        PhotoPath = wp.PhotoPath,
        Type = wp.Type.ToString().ToLower(),
        NearbyPlaceId = wp.NearbyPlaceId
    };
}