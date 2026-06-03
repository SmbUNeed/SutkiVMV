using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMVCS.Services;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/apartments/{aptId:int}/photos")]
[Authorize(Roles = "Admin")]
public class PhotosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly string _uploadRoot; // абсолютный путь к data/uploads

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public PhotosController(AppDbContext context, UploadSettings upload)
    {
        _context = context;
        _uploadRoot = upload.ApartmentsUploadPath;
    }

    [HttpPost("migrate-images")]
    [Authorize(Roles = "Admin")]
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

        await _context.SaveChangesAsync(ct);
        return Ok(new { message = $"Конвертировано: {converted}" });
    }

    // POST api/apartments/{aptId}/photos
    [HttpPost]
    public async Task<ActionResult> Upload(int aptId, CancellationToken ct,
        [FromForm] IFormFileCollection files,
        [FromForm] int mainPhotoIndex = 0)
    {
        var apartment = await _context.Apartments.FindAsync(aptId);
        if (apartment == null) return NotFound(new { message = "Квартира не найдена" });
        if (!files.Any()) return BadRequest(new { message = "Файлы не переданы" });

        // data/uploads/{aptId}/
        var aptDir = Path.Combine(_uploadRoot, aptId.ToString());
        Directory.CreateDirectory(aptDir);

        bool hasExisting = await _context.ApartmentGallery.AnyAsync(g => g.ApartmentId == aptId);
        int saved = 0;

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var fileName = await ImageHelper.SaveAsWebp(file.OpenReadStream(), aptDir);

            // mainPhotoIndex == -1 → не менять главное при добавлении фото в редакторе
            bool isMain = (!hasExisting && i == 0)
                       || (mainPhotoIndex >= 0 && i == mainPhotoIndex);

            if (isMain)
            {
                // Снимаем флаг со старого главного
                await _context.ApartmentGallery
                    .Where(g => g.ApartmentId == aptId && g.IsMain)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsMain, false));
            }

            _context.ApartmentGallery.Add(new ApartmentGallery
            {
                ApartmentId = aptId,
                ImagePath = $"/uploads/{aptId}/{fileName}",
                IsMain = isMain
            });

            hasExisting = true;
            saved++;
        }

        if (saved == 0)
            return BadRequest(new { message = "Ни один файл не прошёл проверку типа" });

        await _context.SaveChangesAsync(ct);
        return Ok(new { message = $"Загружено фото: {saved}" });
    }

    // DELETE api/apartments/{aptId}/photos/{photoId}
    [HttpDelete("{photoId:int}")]
    public async Task<ActionResult> Delete(int aptId, int photoId, CancellationToken ct)
    {
        var photo = await _context.ApartmentGallery
            .FirstOrDefaultAsync(g => g.Id == photoId && g.ApartmentId == aptId);
        if (photo == null) return NotFound();

        // Удаляем файл с диска
        // ImagePath вида /uploads/{aptId}/{file}
        var relPath = photo.ImagePath.TrimStart('/').Substring("uploads/".Length); // "{aptId}/{file}"
        var diskPath = Path.Combine(_uploadRoot, relPath);
        if (System.IO.File.Exists(diskPath)) System.IO.File.Delete(diskPath);

        bool wasMain = photo.IsMain;
        _context.ApartmentGallery.Remove(photo);
        await _context.SaveChangesAsync(ct);

        // Назначаем следующее фото главным
        if (wasMain)
        {
            var next = await _context.ApartmentGallery
                .FirstOrDefaultAsync(g => g.ApartmentId == aptId);
            if (next != null) { next.IsMain = true; await _context.SaveChangesAsync(ct); }
        }

        return NoContent();
    }

    // PATCH api/apartments/{aptId}/photos/{photoId}/set-main
    [HttpPatch("{photoId:int}/set-main")]
    public async Task<ActionResult> SetMain(int aptId, int photoId, CancellationToken ct)
    {
        var photo = await _context.ApartmentGallery
            .FirstOrDefaultAsync(g => g.Id == photoId && g.ApartmentId == aptId);
        if (photo == null) return NotFound();

        await _context.ApartmentGallery
            .Where(g => g.ApartmentId == aptId && g.IsMain)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsMain, false));

        photo.IsMain = true;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}