using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;

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

    // 1. GET: api/apartments (Получить все квартиры)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Apartment>>> GetApartments()
    {
        return await _context.Apartments.ToListAsync();
    }

    // 2. POST: api/apartments (Добавить новую квартиру)
    [HttpPost]
    public async Task<ActionResult<Apartment>> PostApartment(Apartment apartment)
    {
        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync();
        return Ok(apartment);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteApartmentById(int id)
    {
        Apartment? apartment = await _context.Apartments.FindAsync(id);

        if (apartment == null) return NotFound(new { message = $"Квартира с id={id} не найден(а)" });

        _context.Apartments.Remove(apartment);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("by_title/{title}")]
    public async Task<ActionResult> DeleteApartmentByTitle(string title)
    {
        Apartment? apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.Title == title);

        if (apartment == null) return NotFound(new { message = $"Квартира {title} не найден(а)" });

        _context.Apartments.Remove(apartment);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}