using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Png;

namespace BusSystem.FileStorage.API.Services
{
    public interface IImageOptimizationService
    {
        Task<(byte[] optimizedImage, string format)> OptimizeImageAsync(Stream imageStream, int maxWidth = 800, int maxHeight = 800, int quality = 90);
        Task<byte[]> ConvertToWebPAsync(Stream imageStream, int quality = 90);
    }

    public class ImageOptimizationService : IImageOptimizationService
    {
        private readonly ILogger<ImageOptimizationService> _logger;

        public ImageOptimizationService(ILogger<ImageOptimizationService> logger)
        {
            _logger = logger;
        }

        public async Task<(byte[] optimizedImage, string format)> OptimizeImageAsync(
            Stream imageStream, 
            int maxWidth = 800, 
            int maxHeight = 800, 
            int quality = 90)
        {
            try
            {
                using var image = await Image.LoadAsync(imageStream);
                
                _logger.LogInformation("Original image: {Width}x{Height}", image.Width, image.Height);

                // Check if image has transparency
                bool hasTransparency = ImageHasTransparency(image);

                // Only resize if image is larger than max dimensions
                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(maxWidth, maxHeight),
                        Mode = ResizeMode.Max, // Maintain aspect ratio
                        Sampler = KnownResamplers.Lanczos3 // High quality resampling
                    }));
                    
                    _logger.LogInformation("Resized to: {Width}x{Height}", image.Width, image.Height);
                }

                using var outputStream = new MemoryStream();
                
                // Smart format selection:
                // 1. If has transparency -> PNG (to preserve transparency)
                // 2. Otherwise -> WebP (best quality/size ratio)
                if (hasTransparency)
                {
                    _logger.LogInformation("Image has transparency, saving as PNG");
                    var pngEncoder = new PngEncoder 
                    { 
                        CompressionLevel = PngCompressionLevel.BestCompression 
                    };
                    await image.SaveAsPngAsync(outputStream, pngEncoder);
                    return (outputStream.ToArray(), "png");
                }
                else
                {
                    // Use WebP for best quality/size ratio
                    _logger.LogInformation("Saving as WebP with quality {Quality}", quality);
                    var webpEncoder = new WebpEncoder 
                    { 
                        Quality = quality,
                        Method = WebpEncodingMethod.BestQuality // Better quality
                    };
                    await image.SaveAsWebpAsync(outputStream, webpEncoder);
                    return (outputStream.ToArray(), "webp");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image");
                throw;
            }
        }

        // Helper method to detect transparency
        private bool ImageHasTransparency(Image image)
        {
            try
            {
                // Check if the image format supports transparency
                var formatName = image.Metadata?.DecodedImageFormat?.Name?.ToUpperInvariant();
                
                // PNG and WebP commonly have transparency
                if (formatName == "PNG" || formatName == "WEBP")
                {
                    // For PNG, check if it has an alpha channel
                    return formatName == "PNG";
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ConvertToWebPAsync(Stream imageStream, int quality = 90)
        {
            try
            {
                using var image = await Image.LoadAsync(imageStream);
                using var outputStream = new MemoryStream();
                
                var encoder = new WebpEncoder { Quality = quality };
                await image.SaveAsWebpAsync(outputStream, encoder);
                
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting to WebP");
                throw;
            }
        }
    }
}

