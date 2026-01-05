using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using BusSystem.Admin.Web.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;

namespace BusSystem.Admin.Web.Controllers;

[Authorize]
public class MenuItemManagementController : Controller
{
    private readonly ILogger<MenuItemManagementController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MenuItemManagementController(
        ILogger<MenuItemManagementController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("default");
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

    // GET: MenuItemManagement/Index
    public async Task<IActionResult> Index()
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/menuitems");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch menu items");
                return View(new List<MenuItemDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<MenuItemDto>>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var menuItems = apiResponse?.Data ?? new List<MenuItemDto>();

            // Get categories to enrich menu items
            var categoriesResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories");
            
            // Get buses to enrich menu items with bus plate numbers
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var busesResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            
            if (categoriesResponse.IsSuccessStatusCode && busesResponse.IsSuccessStatusCode)
            {
                var categoriesJson = await categoriesResponse.Content.ReadAsStringAsync();
                var categoriesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryDto>>>(categoriesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var busesJson = await busesResponse.Content.ReadAsStringAsync();
                var busesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(busesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var categories = categoriesApiResponse?.Data ?? new List<CategoryDto>();
                var buses = busesApiResponse?.Data ?? new List<BusDto>();
                
                var categoryDict = categories.ToDictionary(c => c.CategoryId, c => c);
                var busDict = buses.ToDictionary(b => b.BusId, b => b.PlateNumber);

                // Enrich menu items with category names and bus plate numbers
                foreach (var item in menuItems)
                {
                    if (categoryDict.ContainsKey(item.CategoryId))
                    {
                        var category = categoryDict[item.CategoryId];
                        item.CategoryName = category.Name;
                        
                        // Add bus plate number
                        if (busDict.ContainsKey(category.BusId))
                        {
                            item.BusPlateNumber = busDict[category.BusId];
                        }
                    }
                }
            }

            return View(menuItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading menu items");
            return View(new List<MenuItemDto>());
        }
    }

    // GET: MenuItemManagement/Create
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesDropdown();
        return View(new MenuItemDto());
    }

    // POST: MenuItemManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Create(MenuItemDto menuItem, IFormFile? imageFile)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            // Handle image upload if provided
            string? imageUrl = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                _logger.LogInformation("Image file detected for upload: {FileName}", imageFile.FileName);
                imageUrl = await UploadImageAsync(imageFile);
                
                if (imageUrl == null)
                {
                    _logger.LogWarning("Image upload failed for menu item: {Name}", menuItem.Name);
                    ModelState.AddModelError("", "Warning: Image upload failed. The menu item will be created without an image. Please ensure FileStorage service is running.");
                    TempData["WarningMessage"] = "Image upload failed. Please ensure FileStorage service is running on port 5002.";
                }
                else
                {
                    _logger.LogInformation("Image uploaded successfully: {ImageUrl}", imageUrl);
                }
            }

            var createDto = new 
            { 
                CategoryId = menuItem.CategoryId, 
                Name = menuItem.Name, 
                Description = menuItem.Description,
                Price = menuItem.Price,
                Image = imageUrl
            };
            var json = JsonSerializer.Serialize(createDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{menuServiceUrl}/api/menuitems", content);
            
            if (response.IsSuccessStatusCode)
            {
                if (imageUrl != null)
                {
                    TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' has been successfully created with image.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' has been successfully created (without image).";
                }
                _logger.LogInformation("Menu item {Name} created successfully", menuItem.Name);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create menu item. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            ModelState.AddModelError("", $"Failed to create menu item: {response.StatusCode}");
            await LoadCategoriesDropdown();
            return View(menuItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item");
            ModelState.AddModelError("", "An error occurred while creating the menu item. Please try again.");
            await LoadCategoriesDropdown();
            return View(menuItem);
        }
    }

    // GET: MenuItemManagement/Edit/5
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/menuitems/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MenuItemDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (apiResponse?.Data == null)
            {
                return NotFound();
            }

            await LoadCategoriesDropdown();
            return View(apiResponse.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading menu item {MenuItemId}", id);
            return NotFound();
        }
    }

    // POST: MenuItemManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<IActionResult> Edit(int id, MenuItemDto menuItem, IFormFile? imageFile)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            // Handle image upload if provided
            string? imageUrl = menuItem.Image;
            if (imageFile != null && imageFile.Length > 0)
            {
                imageUrl = await UploadImageAsync(imageFile);
            }

            var updateDto = new 
            { 
                CategoryId = menuItem.CategoryId,
                Name = menuItem.Name, 
                Description = menuItem.Description,
                Price = menuItem.Price,
                Image = imageUrl
            };
            var json = JsonSerializer.Serialize(updateDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{menuServiceUrl}/api/menuitems/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Menu item '{menuItem.Name}' has been successfully updated.";
                _logger.LogInformation("Menu item {MenuItemId} updated successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to update menu item {MenuItemId}. Status: {StatusCode}, Error: {Error}", id, response.StatusCode, errorContent);
            ModelState.AddModelError("", $"Failed to update menu item: {response.StatusCode}");
            await LoadCategoriesDropdown();
            return View(menuItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item {MenuItemId}", id);
            ModelState.AddModelError("", "An error occurred while updating the menu item. Please try again.");
            await LoadCategoriesDropdown();
            return View(menuItem);
        }
    }

    // GET: MenuItemManagement/Delete/5
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var response = await _httpClient.GetAsync($"{menuServiceUrl}/api/menuitems/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MenuItemDto>>(json, new JsonSerializerOptions 
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
            _logger.LogError(ex, "Error loading menu item {MenuItemId} for deletion", id);
            return NotFound();
        }
    }

    // POST: MenuItemManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var response = await _httpClient.DeleteAsync($"{menuServiceUrl}/api/menuitems/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Menu item has been successfully deleted.";
                _logger.LogInformation("Menu item {MenuItemId} deleted successfully", id);
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete menu item {MenuItemId}. Status: {StatusCode}, Error: {Error}", id, response.StatusCode, errorContent);
            TempData["ErrorMessage"] = $"Failed to delete menu item: {response.StatusCode}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu item {MenuItemId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the menu item. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: MenuItemManagement/DeleteImage
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin,Manager")]
    public async Task<IActionResult> DeleteImage([FromQuery] int id)
    {
        try
        {
            AttachAuthToken();
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            
            // Get the current menu item to retrieve the image URL
            var getResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/menuitems/{id}");
            
            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch menu item {MenuItemId} for image deletion", id);
                return Json(new { success = false, message = "Menu item not found" });
            }

            var json = await getResponse.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<MenuItemDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var menuItem = apiResponse?.Data;
            if (menuItem == null)
            {
                return Json(new { success = false, message = "Menu item not found" });
            }

            // If there's an image, try to delete it from FileStorage service
            if (!string.IsNullOrWhiteSpace(menuItem.Image))
            {
                await DeleteImageFromStorageAsync(menuItem.Image);
            }

            // Update the menu item to remove the image URL
            var updateDto = new 
            { 
                CategoryId = menuItem.CategoryId,
                Name = menuItem.Name, 
                Description = menuItem.Description,
                Price = menuItem.Price,
                Image = (string?)null  // Set image to null
            };
            var updateJson = JsonSerializer.Serialize(updateDto);
            var content = new StringContent(updateJson, Encoding.UTF8, "application/json");
            
            var updateResponse = await _httpClient.PutAsync($"{menuServiceUrl}/api/menuitems/{id}", content);
            
            if (updateResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Image deleted successfully for menu item {MenuItemId}", id);
                return Json(new { success = true, message = "Image deleted successfully" });
            }
            
            var errorContent = await updateResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to update menu item {MenuItemId} after image deletion. Status: {StatusCode}, Error: {Error}", 
                id, updateResponse.StatusCode, errorContent);
            return Json(new { success = false, message = "Failed to update menu item" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image for menu item {MenuItemId}", id);
            return Json(new { success = false, message = "An error occurred while deleting the image" });
        }
    }

    private async Task DeleteImageFromStorageAsync(string imageUrl)
    {
        try
        {
            var fileStorageServiceUrl = _configuration["ApiUrls:FileStorageService"];
            
            if (string.IsNullOrEmpty(fileStorageServiceUrl) || string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            // Get JWT token from session
            var token = HttpContext.Session.GetString("jwt");
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("JWT token not found in session. Cannot delete image from storage.");
                return;
            }

            // Extract the file URL path from the full URL
            // Example: http://localhost:5002/files/menuitems/abc.jpg -> /files/menuitems/abc.jpg
            var uri = new Uri(imageUrl);
            var fileUrlPath = uri.AbsolutePath;

            _logger.LogInformation("Attempting to delete file from storage: {FileUrl}", fileUrlPath);

            var deleteUrl = $"{fileStorageServiceUrl}/api/files/delete?fileUrl={Uri.EscapeDataString(fileUrlPath)}";
            
            // Create request with Authorization header
            var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            request.Headers.Add("Authorization", $"Bearer {token}");
            
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File deleted successfully from storage: {FileUrl}", fileUrlPath);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to delete file from storage. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting image from FileStorage service. Image URL: {ImageUrl}", imageUrl);
            // Don't throw - we still want to update the menu item even if file deletion fails
        }
    }

    private async Task LoadCategoriesDropdown()
    {
        try
        {
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            
            // Fetch categories
            var categoriesResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories");
            
            // Fetch buses
            var busesResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            
            if (categoriesResponse.IsSuccessStatusCode && busesResponse.IsSuccessStatusCode)
            {
                var categoriesJson = await categoriesResponse.Content.ReadAsStringAsync();
                var categoriesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryDto>>>(categoriesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var busesJson = await busesResponse.Content.ReadAsStringAsync();
                var busesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(busesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var categories = categoriesApiResponse?.Data ?? new List<CategoryDto>();
                var buses = busesApiResponse?.Data ?? new List<BusDto>();
                
                // Create a dictionary of bus ID to plate number
                var busPlateNumbers = buses.ToDictionary(b => b.BusId, b => b.PlateNumber);
                
                // Create a list with combined category name and bus plate number
                var categoriesWithBus = categories.Select(c => new
                {
                    c.CategoryId,
                    DisplayName = busPlateNumbers.ContainsKey(c.BusId) 
                        ? $"{c.Name} - {busPlateNumbers[c.BusId]}" 
                        : c.Name
                }).ToList();
                
                ViewBag.Categories = new SelectList(categoriesWithBus, "CategoryId", "DisplayName");
            }
            else
            {
                ViewBag.Categories = new SelectList(new List<CategoryDto>(), "CategoryId", "Name");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories dropdown");
            ViewBag.Categories = new SelectList(new List<CategoryDto>(), "CategoryId", "Name");
        }
    }

    private async Task<string?> UploadImageAsync(IFormFile imageFile)
    {
        try
        {
            var fileStorageServiceUrl = _configuration["ApiUrls:FileStorageService"];
            
            if (string.IsNullOrEmpty(fileStorageServiceUrl))
            {
                _logger.LogError("FileStorageService URL is not configured in appsettings.json");
                return null;
            }

            _logger.LogInformation("Attempting to upload file: {FileName} ({Size} bytes) to {ServiceUrl}", 
                imageFile.FileName, imageFile.Length, fileStorageServiceUrl);
            
            // Get JWT token from session
            var token = HttpContext.Session.GetString("jwt");
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("JWT token not found in session. User must be authenticated.");
                return null;
            }
            
            // Create form data for file upload
            using var formData = new MultipartFormDataContent();
            using var fileContent = new StreamContent(imageFile.OpenReadStream());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(imageFile.ContentType);
            
            formData.Add(fileContent, "file", imageFile.FileName);
            
            // Upload to FileStorage service with folder parameter
            var uploadUrl = $"{fileStorageServiceUrl}/api/files/upload?folder=menuitems";
            _logger.LogInformation("Uploading to URL: {UploadUrl}", uploadUrl);
            
            // Create request with Authorization header
            var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Content = formData;
            request.Headers.Add("Authorization", $"Bearer {token}");
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to upload file to FileStorage service. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                return null;
            }
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Upload response: {Response}", responseJson);
            
            var uploadResponse = JsonSerializer.Deserialize<FileUploadResponse>(responseJson, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (uploadResponse?.Success == true && uploadResponse.Data?.FileUrl != null)
            {
                // Return the full URL to the FileStorage service
                var fullImageUrl = $"{fileStorageServiceUrl}{uploadResponse.Data.FileUrl}";
                _logger.LogInformation("File uploaded successfully. URL: {ImageUrl}", fullImageUrl);
                return fullImageUrl;
            }
            
            _logger.LogError("File upload response was not successful. Response: {Response}", responseJson);
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error uploading image to FileStorage service. Is the FileStorage service running on port 5002?");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to FileStorage service");
            return null;
        }
    }

    private class FileUploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public FileUploadData? Data { get; set; }
    }

    private class FileUploadData
    {
        public string? FileName { get; set; }
        public string? OriginalFileName { get; set; }
        public string? FileUrl { get; set; }
        public long FileSize { get; set; }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    private class BusDto
    {
        public int BusId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
    }
}
