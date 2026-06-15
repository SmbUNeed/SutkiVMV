using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Services;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = "Admin")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ClientsController(AppDbContext context) => _context = context;

    // GET /api/clients
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClientDto>>> GetAll()
    {
        var clients = await _context.Clients.AsNoTracking().ToListAsync();
        return Ok(clients.Select(ToDto));
    }

    // GET /api/clients/search?q=Иван
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ClientDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<ClientDto>());

        q = q.Trim().ToLower();
        var clients = await _context.Clients
            .Where(c => c.FullName.ToLower().Contains(q)
                     || c.PhoneNumber.Contains(q)
                     || c.Email.ToLower().Contains(q))
            .Take(20)
            .AsNoTracking()
            .ToListAsync();

        return Ok(clients.Select(ToDto));
    }

    // GET /api/clients/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> GetById(int id)
    {
        var c = await _context.Clients.FindAsync(id);
        return c == null ? NotFound() : Ok(ToDto(c));
    }

    // POST /api/clients
    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(CreateClientDto dto, CancellationToken ct)
    {
        var client = new Client
        {
            FullName = dto.FullName.Trim(),
            PhoneNumber = PhoneHelper.Normalize(dto.PhoneNumber.Trim())!,
            Email = dto.Email?.Trim(),
            AdminTag = dto.AdminTag?.Trim(),
            AdminNote = dto.AdminNote?.Trim(),
            InBlackList = dto.InBlackList
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(client));
    }

    // PUT /api/clients/{id}
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, Client updated, CancellationToken ct)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound();
        client.FullName = updated.FullName;
        client.PhoneNumber = PhoneHelper.Normalize(updated.PhoneNumber)!;
        client.Email = updated.Email;
        client.AdminTag = updated.AdminTag;
        client.AdminNote = updated.AdminNote;
        client.InBlackList = updated.InBlackList;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/clients/{id}
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound();
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ClientDto ToDto(Client c) => new()
    {
        Id = c.Id,
        FullName = c.FullName,
        PhoneNumber = c.PhoneNumber,
        Email = c.Email,
        UserId = c.UserId,
        AdminTag = c.AdminTag,
        AdminNote = c.AdminNote,
        InBlackList = c.InBlackList,
    };
}