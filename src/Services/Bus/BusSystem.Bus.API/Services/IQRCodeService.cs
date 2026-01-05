namespace BusSystem.Bus.API.Services;

public interface IQRCodeService
{
    Task<string> GenerateQRCodeAsync(int busId, string plateNumber);
    Task DeleteQRCodeAsync(string qrCodeUrl);
}

