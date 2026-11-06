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
        private readonly AppDbContext _ctx;
        private readonly string _appUploadPath;

        public AdminController(AppDbContext ctx, UploadSettings uploadSettings) {
            _ctx = ctx;
            _appUploadPath = uploadSettings.AppUploadPath;
        }

        [HttpPost("trigram-client-names")]
        public async Task<IActionResult> TrigramClientNames() {
            return Ok();
        }

        [HttpGet("app/latest")]
        public async Task<IActionResult> GetLatestAppVersion() {
            var latest = await _ctx.AppReleases.OrderByDescending(r => r.VersionCode).FirstOrDefaultAsync();

            if (latest == null) return NotFound(new {message = "Релизы не найдены"});

            string downloadUrl = $"https://sutkivmv.ru/api/admin/app/download/{latest.VersionCode}";

            return Ok(new
            {
                latest.VersionCode,
                latest.VersionName,
                latest.ReleaseNotes,
                downloadUrl
            });
        }

        [HttpGet("app/download/{versionCode:int}")]
        public async Task<IActionResult> DownloadApk(int versionCode) {
            var release = await _ctx.AppReleases.FirstOrDefaultAsync(r => r.VersionCode == versionCode);
            if (release == null) return NotFound(new { message = $"Релизов versionCode={versionCode} не найдено" });

            var filePath = Path.Combine(_appUploadPath, release.FileName);
            if (!System.IO.File.Exists(filePath)) {
                return NotFound(new { message = "Файл не найден на сервере" });
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(fileStream, "application/vnd.android.package-archive", release.FileName, enableRangeProcessing: true);
        }

        [HttpPost("app/upload")]
        public async Task<IActionResult> UploadApk([FromForm] int versionCode, [FromForm] string versionName, [FromForm] string releaseNotes, IFormFile file) {
            if (file == null || !file.FileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Требуется файл .apk");

            var uniqueFileName = $"app-{versionCode}-{Guid.NewGuid():N}.apk";
            var filePath = Path.Combine(_appUploadPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(stream);
            }

            var release = new AppRelease
            {
                VersionCode = versionCode,
                VersionName = versionName,
                FileName = uniqueFileName,
                ReleaseNotes = releaseNotes,
            };
            _ctx.AppReleases.Add(release);
            await _ctx.SaveChangesAsync();

            return Ok("Приложение успешно загружено");
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
                    b.ClientId,
                    b.ClientName,
                    b.Source,
                    StartDate = b.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = b.EndDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return Ok(bookings);
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
