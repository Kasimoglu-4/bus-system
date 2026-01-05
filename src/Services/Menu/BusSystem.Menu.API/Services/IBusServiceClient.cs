namespace BusSystem.Menu.API.Services;

public interface IBusServiceClient
{
    Task<bool> BusExistsAsync(int busId);
    Task<BusInfo?> GetBusInfoAsync(int busId);
}

public record BusInfo(int BusId, string PlateNumber, string? Description);

