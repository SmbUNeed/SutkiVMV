using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SutkiVMVCS.Services
{
    public static class ImageHelper
    {
        public static async Task<string> SaveAsWebp(Stream inputStream, string directory, int maxWidth = 1200)
        {
            var filename = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(directory, filename);

            using var image = await Image.LoadAsync(inputStream);

            if (image.Width > maxWidth)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, 0)
                }));
            }

            await image.SaveAsWebpAsync(filePath, new WebpEncoder { Quality = 80 });

            return filename;
        }
    }
}
