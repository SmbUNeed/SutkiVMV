using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using System.Security.Claims;

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
        [Authorize]
        public async Task<IActionResult> Register([FromBody] DeviceTokenDto dto)
        {
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId);

            var existing = await _context.DeviceTokens.FirstOrDefaultAsync(t => t.UserId == userId);
            if (existing != null)
            {
                existing.Token = dto.Token;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = dto.Token });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    public record DeviceTokenDto(string Token);
}
