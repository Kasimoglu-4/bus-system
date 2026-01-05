using System.Text.Json;

namespace BusSystem.Menu.API.Services;

public class BusServiceClient : IBusServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BusServiceClient> _logger;

    public BusServiceClient(HttpClient httpClient, ILogger<BusServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> BusExistsAsync(int busId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/buses/{busId}/exists");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bus service returned {StatusCode} for bus {BusId}", 
                    response.StatusCode, busId);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return apiResponse?.Data ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if bus {BusId} exists", busId);
            return false;
        }
    }

    public async Task<BusInfo?> GetBusInfoAsync(int busId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/buses/{busId}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bus service returned {StatusCode} for bus {BusId}", 
                    response.StatusCode, busId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Data == null)
                return null;

            return new BusInfo(
                apiResponse.Data.BusId,
                apiResponse.Data.PlateNumber,
                apiResponse.Data.Description
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bus info for {BusId}", busId);
            return null;
        }
    }

    private record ApiResponse<T>(bool Success, T? Data, string? Message);
    private record BusDto(int BusId, string PlateNumber, string? QRCodeUrl, 
        DateTime CreatedDate, string? Description, int CreatedBy);
}

