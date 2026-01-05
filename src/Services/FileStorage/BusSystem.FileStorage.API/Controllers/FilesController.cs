using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BusSystem.FileStorage.API.Services;

namespace BusSystem.FileStorage.API.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Manager")]
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly ILogger<FilesController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IImageOptimizationService _imageOptimizationService;
    private readonly string _uploadsPath;

    public FilesController(
        ILogger<FilesController> logger, 
        IConfiguration configuration,
        IImageOptimizationService imageOptimizationService)
    {
        _logger = logger;
        _configuration = configuration;
        _imageOptimizationService = imageOptimizationService;
        _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    }

    /// <summary>
    /// Upload a file with automatic image optimization
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<IActionResult> Upload(
        IFormFile file, 
        [FromQuery] string? folder = null,
        [FromQuery] bool optimize = true,
        [FromQuery] int maxWidth = 800,
        [FromQuery] int maxHeight = 800,
        [FromQuery] int quality = 90)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Success = false, Message = "No file provided" });
            }

            // Validate file extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { Success = false, Message = "Invalid file type. Only image files are allowed." });
            }

            // Create folder path if specified
            var targetFolder = string.IsNullOrWhiteSpace(folder) ? _uploadsPath : Path.Combine(_uploadsPath, folder);
            Directory.CreateDirectory(targetFolder);

            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var originalSize = file.Length;
            string uniqueFileName;
            string filePath;
            long optimizedSize = originalSize;

            // Optimize image if requested and it's an image format
            if (optimize && (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png"))
            {
                _logger.LogInformation("Optimizing image: {FileName}, Original size: {Size} KB", 
                    file.FileName, originalSize / 1024);

                using var fileStream = file.OpenReadStream();
                var (optimizedImage, format) = await _imageOptimizationService.OptimizeImageAsync(
                    fileStream, maxWidth, maxHeight, quality);

                optimizedSize = optimizedImage.Length;
                var savedBytes = originalSize - optimizedSize;
                var savedPercent = (savedBytes * 100.0) / originalSize;

                _logger.LogInformation("Image optimized: Saved {SavedKB} KB ({Percent:F1}%)", 
                    savedBytes / 1024, savedPercent);

                // Save optimized image
                uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}.{format}";
                filePath = Path.Combine(targetFolder, uniqueFileName);
                await System.IO.File.WriteAllBytesAsync(filePath, optimizedImage);
            }
            else
            {
                // Save file without optimization
                uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                filePath = Path.Combine(targetFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            // Generate URL
            var fileUrl = string.IsNullOrWhiteSpace(folder) 
                ? $"/files/{uniqueFileName}" 
                : $"/files/{folder}/{uniqueFileName}";

            _logger.LogInformation("File uploaded successfully: {FileName} -> {FileUrl}", file.FileName, fileUrl);

            return Ok(new 
            { 
                Success = true, 
                Message = "File uploaded successfully",
                Data = new 
                {
                    FileName = uniqueFileName,
                    OriginalFileName = file.FileName,
                    FileUrl = fileUrl,
                    OriginalSize = originalSize,
                    OptimizedSize = optimizedSize,
                    SavingsPercent = optimize ? ((originalSize - optimizedSize) * 100.0) / originalSize : 0,
                    Optimized = optimize
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { Success = false, Message = "An error occurred while uploading the file" });
        }
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    [HttpDelete("delete")]
    public IActionResult Delete([FromQuery] string fileUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return BadRequest(new { Success = false, Message = "File URL is required" });
            }

            // Extract file path from URL
            var filePath = fileUrl.Replace("/files/", "");
            var fullPath = Path.Combine(_uploadsPath, filePath);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { Success = false, Message = "File not found" });
            }

            System.IO.File.Delete(fullPath);
            _logger.LogInformation("File deleted successfully: {FileUrl}", fileUrl);

            return Ok(new { Success = true, Message = "File deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file");
            return StatusCode(500, new { Success = false, Message = "An error occurred while deleting the file" });
        }
    }

    /// <summary>
    /// Get file info
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetFileInfo([FromQuery] string fileUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return BadRequest(new { Success = false, Message = "File URL is required" });
            }

            var filePath = fileUrl.Replace("/files/", "");
            var fullPath = Path.Combine(_uploadsPath, filePath);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { Success = false, Message = "File not found" });
            }

            var fileInfo = new FileInfo(fullPath);

            return Ok(new 
            { 
                Success = true, 
                Data = new 
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    FileUrl = fileUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info");
            return StatusCode(500, new { Success = false, Message = "An error occurred while getting file info" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new 
        { 
            Status = "Healthy", 
            Service = "FileStorage.API",
            Timestamp = DateTime.UtcNow 
        });
    }
}

