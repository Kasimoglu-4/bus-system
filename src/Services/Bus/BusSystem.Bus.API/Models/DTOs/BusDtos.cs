namespace BusSystem.Bus.API.Models.DTOs;

public record BusCreateDto(string PlateNumber, string? Description);

public record BusUpdateDto(string? PlateNumber, string? Description);

public record BusReadDto(
    int BusId,
    string PlateNumber,
    string? QRCodeUrl,
    DateTime CreatedDate,
    string? Description,
    int CreatedBy
);

