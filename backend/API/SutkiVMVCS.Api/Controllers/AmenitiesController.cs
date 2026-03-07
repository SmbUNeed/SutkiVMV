using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/amenities")]
public class AmenitiesController : ControllerBase
{
    private readonly AppDbContext _context;
    public AmenitiesController(AppDbContext context) => _context = context;

    // GET /api/amenities — публичный (нужен фронту для отображения)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AmenityDto>>> GetAll()
    {
        var list = await _context.Amenities.AsNoTracking().ToListAsync();
        return Ok(list.Select(a => new AmenityDto { Id = a.Id, Name = a.Name, Icon = a.Icon }));
    }

    // POST /api/amenities
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AmenityDto>> Create(CreateAmenityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Название удобства не может быть пустым" });

        var amenity = new Amenity { Name = dto.Name.Trim(), Icon = dto.Icon.Trim() };
        _context.Amenities.Add(amenity);
        await _context.SaveChangesAsync();
        return Ok(new AmenityDto { Id = amenity.Id, Name = amenity.Name, Icon = amenity.Icon });
    }

    // DELETE /api/amenities/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var amenity = await _context.Amenities.FindAsync(id);
        if (amenity == null) return NotFound();
        _context.Amenities.Remove(amenity);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}