using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using BusSystem.Admin.Web.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;

namespace BusSystem.Admin.Web.Controllers;

[Authorize]
public class CategoryManagementController : Controller
{
    private readonly ILogger<CategoryManagementController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CategoryManagementController(
        ILogger<CategoryManagementController> logger,
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

    // GET: CategoryManagement/Index
    public async Task<IActionResult> Index()
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch categories");
                return View(new List<CategoryDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryDto>>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var categories = apiResponse?.Data ?? new List<CategoryDto>();

            // Get bus details for each category
            var busesResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            if (busesResponse.IsSuccessStatusCode)
            {
                var busesJson = await busesResponse.Content.ReadAsStringAsync();
                var busesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(busesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var buses = busesApiResponse?.Data ?? new List<BusDto>();
                var busDict = buses.ToDictionary(b => b.BusId, b => b.PlateNumber);

                // Create enriched view model
                var enrichedCategories = categories.Select(c => new CategoryWithBusDto
                {
                    CategoryId = c.CategoryId,
                    BusId = c.BusId,
                    Name = c.Name,
                    MenuItemCount = c.MenuItemCount,
                    BusPlateNumber = busDict.ContainsKey(c.BusId) ? busDict[c.BusId] : "Unknown"
                }).ToList();

                return View(enrichedCategories);
            }

            return View(new List<CategoryWithBusDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
            return View(new List<CategoryWithBusDto>());
        }
    }

    // GET: CategoryManagement/Create
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Create(int? busId)
    {
        await LoadBusesDropdown();
        var category = new CategoryDto();
        if (busId.HasValue)
        {
            category.BusId = busId.Value;
        }
        return View(category);
    }

    // POST: CategoryManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Create(CategoryDto category)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            var createDto = new { BusId = category.BusId, Name = category.Name };
            var json = JsonSerializer.Serialize(createDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{menuServiceUrl}/api/categories", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Category '{category.Name}' has been successfully created.";
                _logger.LogInformation("Category {Name} created successfully", category.Name);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create category. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            
            // Parse error message if available
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                ModelState.AddModelError("", errorResponse?.Message ?? $"Failed to create category: {response.StatusCode}");
            }
            catch
            {
                ModelState.AddModelError("", $"Failed to create category: {response.StatusCode}");
            }
            
            await LoadBusesDropdown();
            return View(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            ModelState.AddModelError("", "An error occurred while creating the category. Please try again.");
            await LoadBusesDropdown();
            return View(category);
        }
    }
    
    private class ErrorResponse
    {
        public string? Message { get; set; }
    }

    // GET: CategoryManagement/Edit/5
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<CategoryDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (apiResponse?.Data == null)
            {
                return NotFound();
            }

            await LoadBusesDropdown();
            return View(apiResponse.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading category {CategoryId}", id);
            return NotFound();
        }
    }

    // POST: CategoryManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<IActionResult> Edit(int id, CategoryDto category)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            var updateDto = new { Name = category.Name };
            var json = JsonSerializer.Serialize(updateDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{menuServiceUrl}/api/categories/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Category '{category.Name}' has been successfully updated.";
                _logger.LogInformation("Category {CategoryId} updated successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to update category {CategoryId}. Status: {StatusCode}, Error: {Error}", id, response.StatusCode, errorContent);
            ModelState.AddModelError("", $"Failed to update category: {response.StatusCode}");
            await LoadBusesDropdown();
            return View(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category {CategoryId}", id);
            ModelState.AddModelError("", "An error occurred while updating the category. Please try again.");
            await LoadBusesDropdown();
            return View(category);
        }
    }

    // GET: CategoryManagement/Delete/5
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<CategoryDto>>(json, new JsonSerializerOptions 
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
            _logger.LogError(ex, "Error loading category {CategoryId} for deletion", id);
            return NotFound();
        }
    }

    // POST: CategoryManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            string? categoryName = null;
            
            // Get category details before deletion for the success message
            AttachAuthToken();
            var categoryResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories/{id}");
            
            if (categoryResponse.IsSuccessStatusCode)
            {
                var json = await categoryResponse.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<CategoryDto>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                categoryName = apiResponse?.Data?.Name;
            }
            
            // Attach token again before DELETE request
            AttachAuthToken();
            var response = await _httpClient.DeleteAsync($"{menuServiceUrl}/api/categories/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = categoryName != null 
                    ? $"Category '{categoryName}' (ID: {id}) has been successfully deleted."
                    : $"Category with ID {id} has been successfully deleted.";
                _logger.LogInformation("Category {CategoryId} deleted successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete category {CategoryId}. Status: {StatusCode}, Error: {Error}", 
                id, response.StatusCode, errorContent);
            
            TempData["ErrorMessage"] = $"Failed to delete category. Error: {response.StatusCode}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the category. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task LoadBusesDropdown()
    {
        try
        {
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var response = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var buses = apiResponse?.Data ?? new List<BusDto>();
                ViewBag.Buses = new SelectList(buses, "BusId", "PlateNumber");
            }
            else
            {
                ViewBag.Buses = new SelectList(new List<BusDto>(), "BusId", "PlateNumber");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading buses dropdown");
            ViewBag.Buses = new SelectList(new List<BusDto>(), "BusId", "PlateNumber");
        }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    public class CategoryWithBusDto : CategoryDto
    {
        public string BusPlateNumber { get; set; } = string.Empty;
    }
}
