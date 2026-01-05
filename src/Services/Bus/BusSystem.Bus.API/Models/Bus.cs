using System.ComponentModel.DataAnnotations;

namespace BusSystem.Bus.API.Models;

public class Bus
{
    public int BusId { get; set; }
    
    [Required]
    public required string PlateNumber
    {
        get => _plateNumber;
        set => _plateNumber = (value ?? string.Empty).Trim().ToUpperInvariant();
    }
    private string _plateNumber = string.Empty;
    
    public string? QRCodeUrl { get; set; }
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public string? Description { get; set; }
    
    public int CreatedBy { get; set; } // Reference to user ID from Identity Service
}

