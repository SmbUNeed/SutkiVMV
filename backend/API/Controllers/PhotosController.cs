using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/apartments/{aptId:int}/photos")]
[Authorize(Roles = "Admin")]
public class PhotosController : ControllerBase
{
    private readonly AppDbContext        _context;
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public PhotosController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env     = env;
    }

    // POST api/apartments/{aptId}/photos
    [HttpPost]
    public async Task<ActionResult> Upload(int aptId,
        [FromForm] IFormFileCollection files,
        [FromForm] int mainPhotoIndex = 0)
    {
        var apartment = await _context.Apartments.FindAsync(aptId);
        if (apartment == null) return NotFound();
        if (!files.Any()) return BadRequest(new { message = "Файлы не переданы" });

        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", aptId.ToString());
        Directory.CreateDirectory(uploadDir);

        bool hasExistingPhotos = await _context.ApartmentGallery.AnyAsync(g => g.ApartmentId == aptId);

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var ext  = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream);

            // mainPhotoIndex = -1 означает "не менять текущее главное"
            bool isMain = (!hasExistingPhotos && i == 0)
                       || (mainPhotoIndex >= 0 && i == mainPhotoIndex);

            if (isMain)
            {
                await _context.ApartmentGallery
                    .Where(g => g.ApartmentId == aptId && g.IsMain)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsMain, false));
            }

            _context.ApartmentGallery.Add(new ApartmentGallery
            {
                ApartmentId = aptId,
                ImagePath   = $"/uploads/{aptId}/{fileName}",
                IsMain      = isMain
            });

            hasExistingPhotos = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Фото загружены" });
    }

    // DELETE api/apartments/{aptId}/photos/{photoId}
    [HttpDelete("{photoId:int}")]
    public async Task<ActionResult> Delete(int aptId, int photoId)
    {
        var photo = await _context.ApartmentGallery
            .FirstOrDefaultAsync(g => g.Id == photoId && g.ApartmentId == aptId);
        if (photo == null) return NotFound();

        var fullPath = Path.Combine(_env.WebRootPath, photo.ImagePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

        bool wasMain = photo.IsMain;
        _context.ApartmentGallery.Remove(photo);
        await _context.SaveChangesAsync();

        if (wasMain)
        {
            var next = await _context.ApartmentGallery.FirstOrDefaultAsync(g => g.ApartmentId == aptId);
            if (next != null) { next.IsMain = true; await _context.SaveChangesAsync(); }
        }

        return NoContent();
    }

    // PATCH api/apartments/{aptId}/photos/{photoId}/set-main
    [HttpPatch("{photoId:int}/set-main")]
    public async Task<ActionResult> SetMain(int aptId, int photoId)
    {
        var photo = await _context.ApartmentGallery
            .FirstOrDefaultAsync(g => g.Id == photoId && g.ApartmentId == aptId);
        if (photo == null) return NotFound();

        await _context.ApartmentGallery
            .Where(g => g.ApartmentId == aptId && g.IsMain)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsMain, false));

        photo.IsMain = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
