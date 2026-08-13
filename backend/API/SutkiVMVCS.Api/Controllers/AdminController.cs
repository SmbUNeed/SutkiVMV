using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMVCS.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private AppDbContext _ctx;

        public AdminController(AppDbContext ctx) {
            _ctx = ctx;
        }

        [HttpGet("price-rules")]
        public async Task<ActionResult<List<PriceRule>>> GetPriceRules(CancellationToken ct){
            return await _ctx.PriceRules.ToListAsync(ct);
        }

        [HttpPost("price-rules")]
        public async Task<ActionResult> CreatePriceRule(CreatePriceRuleDto dto, CancellationToken ct) {
            if (await _ctx.PriceRules.AnyAsync(pr => pr.StartDate == dto.StartDate)) return Conflict("В правилах уже есть выбранная дата");
            
            var priceRule = new PriceRule()
            {
                ApartmentId = dto.ApartmentId,
                Label = dto.Label,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsRecurringYearly = dto.IsRecurringYearly,
                Multiplier = dto.Multiplier,
                FixedMarkup = dto.FixedMarkup
            };

            await _ctx.PriceRules.AddAsync(priceRule);
            await _ctx.SaveChangesAsync();

            return Ok();
        }

        [HttpPut("price-rules/{id:int}")]
        public async Task<ActionResult> UpdatePriceRule(int id, CreatePriceRuleDto dto, CancellationToken ct)
        {
            var pr = await _ctx.PriceRules.FirstAsync(pr => pr.Id == id);
            if (pr == null) return NotFound("Правило не найдено");

            pr.ApartmentId = dto.ApartmentId;
            pr.Label = dto.Label;
            pr.StartDate = dto.StartDate;
            pr.EndDate = dto.EndDate;
            pr.IsRecurringYearly = dto.IsRecurringYearly;
            pr.Multiplier = dto.Multiplier;
            pr.FixedMarkup = dto.FixedMarkup;

            await _ctx.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("price-rules/{id:int}")]
        public async Task<ActionResult> DeletePriceRule(int id, CancellationToken ct)
        {
            var pr = await _ctx.PriceRules.FirstAsync(pr => pr.Id == id);
            if (pr == null) return NotFound("Правило не найдено");

            _ctx.PriceRules.Remove(pr);
            await _ctx.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("external-bookings")]
        public async Task<ActionResult<List<ExternalBooking>>> GetExternalBookings() {
            var bookings = await _ctx.ExternalBookings
                .Include(b => b.Apartment)
                .Select(b => new
                {
                    b.Id,
                    b.ApartmentId,
                    ApartmentTitle = b.Apartment.Title,
                    b.Source,
                    StartDate = b.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = b.EndDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return Ok(bookings.OrderBy(b => b.StartDate));
        }

        [HttpPut("external-bookings{id:int}/client")]
        [Authorize (Roles = "Admin")]
        public async Task<ActionResult> PinClientToExternalBooking(int id, int? clientId) {
            var booking = await _ctx.ExternalBookings.FindAsync(id);
            if (booking == null) return NotFound();
            booking.ClientId = clientId;
            await _ctx.SaveChangesAsync();
            return Ok();
        }
    }
}
