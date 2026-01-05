using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BusSystem.Admin.Web.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;

namespace BusSystem.Admin.Web.Controllers;

[Authorize] // All authenticated users can view
public class BusManagementController : Controller
{
    private readonly ILogger<BusManagementController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BusManagementController(
        ILogger<BusManagementController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    private void AttachAuthToken()
    {
        var token = _httpContextAccessor.HttpContext?.Session.GetString("jwt");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    // GET: BusManagement/Index
    public async Task<IActionResult> Index()
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var response = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch buses");
                return View(new List<BusDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return View(apiResponse?.Data ?? new List<BusDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading buses");
            return View(new List<BusDto>());
        }
    }

    // GET: BusManagement/Inactive
    public async Task<IActionResult> Inactive()
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            // Get all buses
            var busesResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            if (!busesResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch buses");
                return View(new List<BusDto>());
            }

            var busesJson = await busesResponse.Content.ReadAsStringAsync();
            var busesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(busesJson, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            var buses = busesApiResponse?.Data ?? new List<BusDto>();

            // Get all categories
            var categoriesResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories");
            var busIdsWithCategories = new HashSet<int>();
            
            if (categoriesResponse.IsSuccessStatusCode)
            {
                var categoriesJson = await categoriesResponse.Content.ReadAsStringAsync();
                var categoriesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryDto>>>(categoriesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                var categories = categoriesApiResponse?.Data ?? new List<CategoryDto>();
                busIdsWithCategories = categories.Select(c => c.BusId).Distinct().ToHashSet();
            }

            // Filter inactive buses (buses without categories)
            var inactiveBuses = buses.Where(b => !busIdsWithCategories.Contains(b.BusId)).ToList();
            
            return View(inactiveBuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inactive buses");
            return View(new List<BusDto>());
        }
    }

    // GET: BusManagement/Create
    [Authorize(Roles = "SuperAdmin,Admin")] // Only SuperAdmin and Admin can create
    public IActionResult Create()
    {
        return View(new BusDto());
    }

    // POST: BusManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")] // Only SuperAdmin and Admin can create
    public async Task<IActionResult> Create(BusDto bus)
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            
            var createDto = new { PlateNumber = bus.PlateNumber, Description = bus.Description };
            var json = JsonSerializer.Serialize(createDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{busServiceUrl}/api/buses", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Bus '{bus.PlateNumber}' has been successfully created with QR code.";
                _logger.LogInformation("Bus {PlateNumber} created successfully", bus.PlateNumber);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create bus. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            ModelState.AddModelError("", $"Failed to create bus: {response.StatusCode}");
            return View(bus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bus");
            ModelState.AddModelError("", "An error occurred while creating the bus. Please try again.");
            return View(bus);
        }
    }

    // GET: BusManagement/Edit/5
    [Authorize(Roles = "SuperAdmin,Admin,Manager")] // All roles can edit
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var response = await _httpClient.GetAsync($"{busServiceUrl}/api/buses/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (apiResponse?.Data == null)
            {
                return NotFound();
            }

            return View(apiResponse.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bus {BusId}", id);
            return NotFound();
        }
    }

    // POST: BusManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")] // All roles can edit
    public async Task<IActionResult> Edit(int id, BusDto bus)
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            
            var updateDto = new { PlateNumber = bus.PlateNumber, Description = bus.Description };
            var json = JsonSerializer.Serialize(updateDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{busServiceUrl}/api/buses/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Bus '{bus.PlateNumber}' has been successfully updated.";
                _logger.LogInformation("Bus {BusId} updated successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to update bus {BusId}. Status: {StatusCode}, Error: {Error}", id, response.StatusCode, errorContent);
            ModelState.AddModelError("", $"Failed to update bus: {response.StatusCode}");
            return View(bus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bus {BusId}", id);
            ModelState.AddModelError("", "An error occurred while updating the bus. Please try again.");
            return View(bus);
        }
    }

    // GET: BusManagement/Delete/5
    [Authorize(Roles = "SuperAdmin,Admin")] // Only SuperAdmin and Admin can delete
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var response = await _httpClient.GetAsync($"{busServiceUrl}/api/buses/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (apiResponse?.Data == null)
            {
                return NotFound();
            }

            return View(apiResponse.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bus {BusId} for deletion", id);
            return NotFound();
        }
    }

    // POST: BusManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")] // Only SuperAdmin and Admin can delete
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            string? plateNumber = null;
            
            // Get bus details before deletion for the success message
            AttachAuthToken();
            var busResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses/{id}");
            
            if (busResponse.IsSuccessStatusCode)
            {
                var json = await busResponse.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                plateNumber = apiResponse?.Data?.PlateNumber;
            }
            
            // Attach token again before DELETE request
            AttachAuthToken();
            var response = await _httpClient.DeleteAsync($"{busServiceUrl}/api/buses/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = plateNumber != null 
                    ? $"Bus '{plateNumber}' (ID: {id}) has been successfully deleted."
                    : $"Bus with ID {id} has been successfully deleted.";
                _logger.LogInformation("Bus {BusId} deleted successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete bus {BusId}. Status: {StatusCode}, Error: {Error}", 
                id, response.StatusCode, errorContent);
            
            TempData["ErrorMessage"] = $"Failed to delete bus. Error: {response.StatusCode}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bus {BusId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the bus. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: BusManagement/DownloadQr/5
    public async Task<IActionResult> DownloadQr(int id)
    {
        try
        {
            AttachAuthToken();
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var response = await _httpClient.GetAsync($"{busServiceUrl}/api/buses/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var bus = apiResponse?.Data;
            if (bus == null || string.IsNullOrEmpty(bus.QRCodeUrl))
            {
                return NotFound();
            }

            // Download QR code from the Bus Service URL (prepend base URL to relative path)
            var qrCodeFullUrl = $"{busServiceUrl}{bus.QRCodeUrl}";
            var qrCodeResponse = await _httpClient.GetAsync(qrCodeFullUrl);
            if (!qrCodeResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download QR code from {Url}", qrCodeFullUrl);
                return NotFound();
            }

            var qrCodeBytes = await qrCodeResponse.Content.ReadAsByteArrayAsync();
            var fileName = $"bus_{id}_{bus.PlateNumber}_qrcode.png";
            
            _logger.LogInformation("QR code downloaded for bus {BusId}: {FileName}", id, fileName);
            
            return File(qrCodeBytes, "image/png", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading QR code for bus {BusId}", id);
            return NotFound();
        }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    private class CategoryDto
    {
        public int CategoryId { get; set; }
        public int BusId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
