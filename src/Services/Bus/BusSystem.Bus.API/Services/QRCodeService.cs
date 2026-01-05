using QRCoder;

namespace BusSystem.Bus.API.Services;

public class QRCodeService : IQRCodeService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QRCodeService> _logger;

    public QRCodeService(
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<QRCodeService> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateQRCodeAsync(int busId, string plateNumber)
    {
        try
        {
            // Get the base URL from configuration or use a default
            var baseUrl = _configuration["QRCode:BaseUrl"] ?? "http://localhost:5155";
            var url = $"{baseUrl}/PlateNumber/{plateNumber}";

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);

            // Create qrcodes directory if it doesn't exist
            var qrCodesFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "qrcodes");
            if (!Directory.Exists(qrCodesFolder))
            {
                Directory.CreateDirectory(qrCodesFolder);
                _logger.LogInformation("Created qrcodes directory at {Path}", qrCodesFolder);
            }

            // Save QR code file
            var fileName = $"bus_{busId}_{plateNumber}.png";
            var filePath = Path.Combine(qrCodesFolder, fileName);
            await File.WriteAllBytesAsync(filePath, qrCodeBytes);

            _logger.LogInformation("Generated QR code for bus {BusId} at {FilePath}", busId, filePath);

            return $"/qrcodes/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code for bus {BusId}", busId);
            throw;
        }
    }

    public async Task DeleteQRCodeAsync(string qrCodeUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(qrCodeUrl))
                return;

            var filePath = Path.Combine(
                _env.WebRootPath ?? "wwwroot",
                qrCodeUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted QR code file at {FilePath}", filePath);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting QR code at {QRCodeUrl}", qrCodeUrl);
            // Don't throw - deletion failure shouldn't prevent bus deletion
        }
    }
}

