using System.Security.Claims;
using BusSystem.Bus.API.Data;
using BusSystem.Bus.API.Models.DTOs;
using BusSystem.Bus.API.Services;
using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using BusSystem.Common.Authorization;
using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusSystem.Bus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusesController : ControllerBase
{
    private readonly BusDbContext _context;
    private readonly IQRCodeService _qrCodeService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BusesController> _logger;

    public BusesController(
        BusDbContext context,
        IQRCodeService qrCodeService,
        IEventBus eventBus,
        ILogger<BusesController> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Get all buses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<BusReadDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<BusReadDto>>>> GetBuses()
    {
        var buses = await _context.Buses
            .AsNoTracking()
            .Select(b => new BusReadDto(
                b.BusId,
                b.PlateNumber,
                b.QRCodeUrl,
                b.CreatedDate,
                b.Description,
                b.CreatedBy
            ))
            .ToListAsync();

        return Ok(ApiResponse<List<BusReadDto>>.SuccessResponse(buses));
    }

    /// <summary>
    /// Get bus by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<BusReadDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<BusReadDto>>> GetBus(int id)
    {
        var bus = await _context.Buses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BusId == id);

        if (bus == null)
        {
            throw new NotFoundException("Bus", id);
        }

        var busDto = new BusReadDto(
            bus.BusId,
            bus.PlateNumber,
            bus.QRCodeUrl,
            bus.CreatedDate,
            bus.Description,
            bus.CreatedBy
        );

        return Ok(ApiResponse<BusReadDto>.SuccessResponse(busDto));
    }

    /// <summary>
    /// Get bus by plate number
    /// </summary>
    [HttpGet("plate/{plateNumber}")]
    [ProducesResponseType(typeof(ApiResponse<BusReadDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<BusReadDto>>> GetBusByPlate(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            throw new ValidationException("Plate number is required");
        }

        var normalizedPlate = plateNumber.Trim().ToUpperInvariant();
        var bus = await _context.Buses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.PlateNumber == normalizedPlate);

        if (bus == null)
        {
            throw new NotFoundException($"Bus with plate number '{plateNumber}' was not found");
        }

        var busDto = new BusReadDto(
            bus.BusId,
            bus.PlateNumber,
            bus.QRCodeUrl,
            bus.CreatedDate,
            bus.Description,
            bus.CreatedBy
        );

        return Ok(ApiResponse<BusReadDto>.SuccessResponse(busDto));
    }

    /// <summary>
    /// Create new bus (SuperAdmin and Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse<BusReadDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<BusReadDto>>> CreateBus([FromBody] BusCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PlateNumber))
        {
            throw new ValidationException("Plate number is required");
        }

        // Get user ID from claims
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new UnauthorizedException();
        }

        var userId = int.Parse(userIdClaim);

        // Check if plate number already exists
        var normalizedPlate = dto.PlateNumber.Trim().ToUpperInvariant();
        var existingBus = await _context.Buses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.PlateNumber == normalizedPlate);

        if (existingBus != null)
        {
            throw new ValidationException($"Bus with plate number '{dto.PlateNumber}' already exists");
        }

        var bus = new Models.Bus
        {
            PlateNumber = dto.PlateNumber.Trim(),
            Description = dto.Description?.Trim(),
            CreatedDate = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Buses.Add(bus);
        await _context.SaveChangesAsync();

        // Generate QR code after bus ID is available
        bus.QRCodeUrl = await _qrCodeService.GenerateQRCodeAsync(bus.BusId, bus.PlateNumber);
        await _context.SaveChangesAsync();

        var busDto = new BusReadDto(
            bus.BusId,
            bus.PlateNumber,
            bus.QRCodeUrl,
            bus.CreatedDate,
            bus.Description,
            bus.CreatedBy
        );

        _logger.LogInformation("Bus {BusId} created with plate number {PlateNumber}", bus.BusId, bus.PlateNumber);

        return CreatedAtAction(
            nameof(GetBus),
            new { id = bus.BusId },
            ApiResponse<BusReadDto>.SuccessResponse(busDto, "Bus created successfully")
        );
    }

    /// <summary>
    /// Update bus (SuperAdmin, Admin, and Manager)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.ContentEditing)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> UpdateBus(int id, [FromBody] BusUpdateDto dto)
    {
        var bus = await _context.Buses.FindAsync(id);
        if (bus == null)
        {
            throw new NotFoundException("Bus", id);
        }

        bool plateNumberChanged = false;
        string? oldQRCodeUrl = bus.QRCodeUrl;

        if (!string.IsNullOrWhiteSpace(dto.PlateNumber))
        {
            var normalizedPlate = dto.PlateNumber.Trim().ToUpperInvariant();
            
            // Check if new plate number already exists (excluding current bus)
            var existingBus = await _context.Buses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.PlateNumber == normalizedPlate && b.BusId != id);

            if (existingBus != null)
            {
                throw new ValidationException($"Bus with plate number '{dto.PlateNumber}' already exists");
            }

            // Check if plate number actually changed
            if (bus.PlateNumber != dto.PlateNumber.Trim())
            {
                plateNumberChanged = true;
            }

            bus.PlateNumber = dto.PlateNumber.Trim();
        }

        if (dto.Description != null)
        {
            bus.Description = dto.Description.Trim();
        }

        await _context.SaveChangesAsync();

        // Regenerate QR code if plate number changed
        if (plateNumberChanged)
        {
            // Delete old QR code
            if (!string.IsNullOrEmpty(oldQRCodeUrl))
            {
                await _qrCodeService.DeleteQRCodeAsync(oldQRCodeUrl);
            }

            // Generate new QR code with updated plate number
            bus.QRCodeUrl = await _qrCodeService.GenerateQRCodeAsync(bus.BusId, bus.PlateNumber);
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR code regenerated for bus {BusId} due to plate number change", id);

            // Publish event only when plate number changes (significant change that might affect other services)
            var busUpdatedEvent = new BusUpdatedEvent(bus.BusId, bus.PlateNumber, bus.Description);
            await _eventBus.PublishAsync(busUpdatedEvent);
            _logger.LogInformation("BusUpdatedEvent published for bus {BusId} due to plate number change", id);
        }

        _logger.LogInformation("Bus {BusId} updated", id);

        return Ok(ApiResponse.SuccessResponse("Bus updated successfully"));
    }

    /// <summary>
    /// Delete bus (SuperAdmin and Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> DeleteBus(int id)
    {
        var bus = await _context.Buses.FindAsync(id);
        if (bus == null)
        {
            throw new NotFoundException("Bus", id);
        }

        // Delete QR code file
        if (!string.IsNullOrEmpty(bus.QRCodeUrl))
        {
            await _qrCodeService.DeleteQRCodeAsync(bus.QRCodeUrl);
        }

        _context.Buses.Remove(bus);
        await _context.SaveChangesAsync();

        // Publish event to notify other services
        var busDeletedEvent = new BusDeletedEvent(bus.BusId);
        await _eventBus.PublishAsync(busDeletedEvent);

        _logger.LogInformation("Bus {BusId} deleted", id);

        return Ok(ApiResponse.SuccessResponse("Bus deleted successfully"));
    }

    /// <summary>
    /// Check if bus exists (for other services to validate)
    /// </summary>
    [HttpGet("{id}/exists")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<ActionResult<ApiResponse<bool>>> BusExists(int id)
    {
        var exists = await _context.Buses.AnyAsync(b => b.BusId == id);
        return Ok(ApiResponse<bool>.SuccessResponse(exists));
    }

    /// <summary>
    /// Regenerate QR code for a bus (SuperAdmin and Admin only)
    /// </summary>
    [HttpPost("{id}/regenerate-qrcode")]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<string>>> RegenerateQRCode(int id)
    {
        var bus = await _context.Buses.FindAsync(id);
        if (bus == null)
        {
            throw new NotFoundException("Bus", id);
        }

        // Delete old QR code if exists
        if (!string.IsNullOrEmpty(bus.QRCodeUrl))
        {
            await _qrCodeService.DeleteQRCodeAsync(bus.QRCodeUrl);
        }

        // Generate new QR code
        bus.QRCodeUrl = await _qrCodeService.GenerateQRCodeAsync(bus.BusId, bus.PlateNumber);
        await _context.SaveChangesAsync();

        _logger.LogInformation("QR code regenerated for bus {BusId}", id);

        return Ok(ApiResponse<string>.SuccessResponse(bus.QRCodeUrl, "QR code regenerated successfully"));
    }
}

