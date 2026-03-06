using Microsoft.AspNetCore.Mvc;
using SutkiVMV.Data;
using SutkiVMV.Models;
using Microsoft.EntityFrameworkCore;

namespace SutkiVMV.Controllers;
[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ClientsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Client>>> GetAll()
        => Ok(await _context.Clients.ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Client>> GetById(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        return client == null ? NotFound() : Ok(client);
    }

    [HttpPost]
    public async Task<ActionResult<Client>> Create(Client client)
    {
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return Ok(client);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Client updated)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound();

        client.FullName = updated.FullName;
        client.PhoneNumber = updated.PhoneNumber;
        client.Email = updated.Email;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound();
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}