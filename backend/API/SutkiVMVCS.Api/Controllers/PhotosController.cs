using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMVCS.Services.Shared;

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

    // POST api/apartments/{aptId}/photos
    [HttpPost]
    public async Task<ActionResult> Upload(int aptId, CancellationToken ct,
        [FromForm] IFormFileCollection files,
        [FromForm] int mainPhotoIndex = 0)
    {
        var apartment = await _context.Apartments.FindAsync(new object[] { aptId }, ct);
        if (apartment == null) return NotFound(new { message = "Квартира не найдена" });
        if (!files.Any()) return BadRequest(new { message = "Файлы не переданы" });

        var aptDir = Path.Combine(_uploadRoot, aptId.ToString());
        Directory.CreateDirectory(aptDir);

        bool hasExisting = await _context.ApartmentGallery.AnyAsync(g => g.ApartmentId == aptId, ct);
        int mainIndex = mainPhotoIndex >= 0 ? mainPhotoIndex : (hasExisting ? -1 : 0);
        var added = new List<ApartmentGallery>();

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            await using var stream = file.OpenReadStream();
            var fileName = await ImageHelper.SaveAsWebp(stream, aptDir);

            added.Add(new ApartmentGallery
            {
                ApartmentId = aptId,
                ImagePath = $"/uploads/{aptId}/{fileName}",
                IsMain = i == mainIndex
            });
        }

        if (added.Count == 0)
            return BadRequest(new { message = "Ни один файл не прошёл проверку типа" });

        if (!hasExisting && !added.Any(p => p.IsMain))
            added[0].IsMain = true;

        if (added.Any(p => p.IsMain))
        {
            await _context.ApartmentGallery
                .Where(g => g.ApartmentId == aptId && g.IsMain)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsMain, false), ct);
        }

        _context.ApartmentGallery.AddRange(added);
        await _context.SaveChangesAsync(ct);
        return Ok(new { message = $"Загружено фото: {added.Count}" });
    }

    // DELETE api/apartments/{aptId}/photos/{photoId}
    [HttpDelete("{photoId:int}")]
    public async Task<ActionResult> Delete(int aptId, int photoId, CancellationToken ct)
    {
        var photo = await _context.ApartmentGallery
            .FirstOrDefaultAsync(g => g.Id == photoId && g.ApartmentId == aptId, ct);
        if (photo == null) return NotFound();

        var oldPath = photo.ImagePath;

        if (photo.IsMain)
        {
            var next = await _context.ApartmentGallery
                .Where(g => g.ApartmentId == aptId && g.Id != photoId)
                .OrderBy(g => g.Id)
                .FirstOrDefaultAsync(ct);
            if (next != null) next.IsMain = true;
        }

        _context.ApartmentGallery.Remove(photo);
        await _context.SaveChangesAsync(ct);

        TryDeleteFile(oldPath);
        return NoContent();
    }

    private void TryDeleteFile(string? imagePath)
    {
        const string prefix = "/uploads/";
        if (string.IsNullOrEmpty(imagePath) || !imagePath.StartsWith(prefix, StringComparison.Ordinal)) return;
        var root = Path.GetFullPath(_uploadRoot) + Path.DirectorySeparatorChar;
        var diskPath = Path.GetFullPath(Path.Combine(_uploadRoot, imagePath.Substring(prefix.Length)));
        if (diskPath.StartsWith(root, StringComparison.Ordinal) && System.IO.File.Exists(diskPath))
            System.IO.File.Delete(diskPath);
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