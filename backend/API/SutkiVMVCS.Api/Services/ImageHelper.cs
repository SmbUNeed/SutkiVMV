using SkiaSharp;

namespace SutkiVMVCS.Services
{
    public static class ImageHelper
    {
        public static async Task<string> SaveAsWebp(Stream inputStream, string directory, int maxWidth = 1200)
        {
            var fileName = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(directory, fileName);

            using var bitmap = SKBitmap.Decode(inputStream);

            var targetWidth = bitmap.Width > maxWidth ? maxWidth : bitmap.Width;
            var targetHeight = bitmap.Width > maxWidth
                ? (int)(bitmap.Height * ((float)maxWidth / bitmap.Width))
                : bitmap.Height;

            using var resized = bitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Webp, 80);

            await using var fileStream = System.IO.File.Create(filePath);
            data.SaveTo(fileStream);

            return fileName;
        }
    }
}
