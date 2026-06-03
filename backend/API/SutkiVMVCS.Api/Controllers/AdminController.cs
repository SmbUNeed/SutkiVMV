using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMVCS.Services;

namespace SutkiVMVCS.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly string _uploadRoot;
        private readonly string _wikiUploadRoot;

        public AdminController(AppDbContext context, UploadSettings upload)
        {
            _context = context;
            _uploadRoot = upload.ApartmentsUploadPath;
            _wikiUploadRoot = upload.WikiUploadPath;
        }

        [HttpPost("migrate-images")]
        public async Task<ActionResult> MigrateImages(CancellationToken ct)
        {
            var gallery = await _context.ApartmentGallery.ToListAsync(ct);
            int converted = 0;

            foreach (var item in gallery)
            {
                // /uploads/{aptId}/{file.jpg}
                var oldRelPath = item.ImagePath.TrimStart('/'); // uploads/{aptId}/{file.jpg}
                var oldDiskPath = Path.Combine(_uploadRoot, oldRelPath.Substring("uploads/".Length));

                if (!System.IO.File.Exists(oldDiskPath)) continue;
                if (oldDiskPath.EndsWith(".webp")) continue; // уже webp

                try
                {
                    var dir = Path.GetDirectoryName(oldDiskPath)!;
                    using var stream = System.IO.File.OpenRead(oldDiskPath);
                    var newFileName = await ImageHelper.SaveAsWebp(stream, dir);

                    // Удаляем старый файл
                    System.IO.File.Delete(oldDiskPath);

                    // Обновляем путь в БД
                    var aptId = Path.GetFileName(dir);
                    item.ImagePath = $"/uploads/{aptId}/{newFileName}";
                    converted++;
                }
                catch { continue; }
            }

            var wikiPlaces = await _context.WikiPlaces
                .Where(w => !string.IsNullOrEmpty(w.PhotoPath))
                .ToListAsync(ct);

            foreach (var place in wikiPlaces)
            {
                var oldRelPath = place.PhotoPath.TrimStart('/'); // wiki_places/{id}/{file}
                var oldDiskPath = Path.Combine(_wikiUploadRoot, oldRelPath.Substring("wiki_places/".Length));

                if (!System.IO.File.Exists(oldDiskPath)) continue;
                if (oldDiskPath.EndsWith(".webp")) continue;

                try
                {
                    var dir = Path.GetDirectoryName(oldDiskPath)!;
                    using var stream = System.IO.File.OpenRead(oldDiskPath);
                    var newFileName = await ImageHelper.SaveAsWebp(stream, dir);

                    System.IO.File.Delete(oldDiskPath);

                    var id = Path.GetFileName(dir);
                    place.PhotoPath = $"/wiki_places/{id}/{newFileName}";
                    converted++;
                } catch { continue; }
            }

            await _context.SaveChangesAsync(ct);

            return Ok(new { message = $"Конвертировано: {converted}" });
        }
    }
}
