using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;

namespace SutkiVMVCS.Controllers
{
    [ApiController]
    [Route("api/device-tokens")]
    [Authorize(Roles = "Admin")]
    public class DeviceTokenController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeviceTokenController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] DeviceTokenDto dto)
        {
            // у нас один админ — просто обновляем единственную запись
            var existing = await _context.DeviceTokens.FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.Token = dto.Token;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken { Token = dto.Token });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    public record DeviceTokenDto(string Token);
}
