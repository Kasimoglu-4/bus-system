using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BusSystem.Admin.Web.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace BusSystem.Admin.Web.Controllers;

[Authorize(Roles = "SuperAdmin")] // Only SuperAdmin can manage users
public class AdminUserManagementController : Controller
{
    private readonly ILogger<AdminUserManagementController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminUserManagementController(
        ILogger<AdminUserManagementController> logger,
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

    private async Task<string?> SaveUploadedImageAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return null;

        try
        {
            // Create uploads directory if it doesn't exist
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Generate unique filename
            var fileExtension = Path.GetExtension(imageFile.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return relative URL path
            return $"/uploads/users/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving uploaded image");
            return null;
        }
    }

    // GET: AdminUserManagement/Index
    public async Task<IActionResult> Index()
    {
        try
        {
            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            var response = await _httpClient.GetAsync($"{identityServiceUrl}/api/Users");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch admin users");
                return View(new List<AdminUserDto>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<AdminUserDto>>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return View(apiResponse?.Data ?? new List<AdminUserDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin users");
            return View(new List<AdminUserDto>());
        }
    }

    // GET: AdminUserManagement/Create
    public IActionResult Create()
    {
        return View(new AdminUserDto());
    }

    // POST: AdminUserManagement/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserDto user, IFormFile? pictureFile)
    {
        try
        {
            // Validate password confirmation
            if (user.Password != user.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match");
                return View(user);
            }

            // Handle image upload
            string? picturePath = await SaveUploadedImageAsync(pictureFile);

            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            
            var createDto = new 
            { 
                UserName = user.UserName, 
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PicturePath = picturePath,
                Password = user.Password,
                Role = user.Role
            };
            var json = JsonSerializer.Serialize(createDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{identityServiceUrl}/api/Users", content);
            
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create admin user: {Error}", errorContent);
            
            // Try to parse the error response to extract a meaningful message
            try
            {
                var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (errorResponse.TryGetProperty("message", out var messageProperty))
                {
                    var message = messageProperty.GetString();
                    if (!string.IsNullOrEmpty(message))
                    {
                        ModelState.AddModelError("", message);
                        return View(user);
                    }
                }
            }
            catch
            {
                // If parsing fails, use generic error
            }
            
            ModelState.AddModelError("", "Failed to create admin user. Please check the details and try again.");
            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin user");
            ModelState.AddModelError("", "An error occurred while creating the user");
            return View(user);
        }
    }

    // GET: AdminUserManagement/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            var response = await _httpClient.GetAsync($"{identityServiceUrl}/api/Users/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<AdminUserDto>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (apiResponse?.Data == null)
            {
                return NotFound();
            }

            var editDto = new AdminUserEditDto
            {
                Id = apiResponse.Data.Id,
                UserName = apiResponse.Data.UserName,
                Email = apiResponse.Data.Email,
                FirstName = apiResponse.Data.FirstName,
                LastName = apiResponse.Data.LastName,
                PicturePath = apiResponse.Data.Picture,
                Role = apiResponse.Data.Role
            };

            return View(editDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin user {UserId}", id);
            return NotFound();
        }
    }

    // POST: AdminUserManagement/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminUserEditDto user, IFormFile? pictureFile)
    {
        try
        {
            // Handle image upload
            string? picturePath = await SaveUploadedImageAsync(pictureFile);
            
            // If no new image was uploaded, keep the existing one
            if (string.IsNullOrEmpty(picturePath))
            {
                picturePath = user.PicturePath;
            }

            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            
            var updateDto = new 
            { 
                UserName = user.UserName, 
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PicturePath = picturePath,
                Role = user.Role,
                CurrentPassword = user.CurrentPassword,
                NewPassword = user.NewPassword
            };
            var json = JsonSerializer.Serialize(updateDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{identityServiceUrl}/api/Users/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                // Check if the edited user is the currently logged-in user
                var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserIdStr != null && int.TryParse(currentUserIdStr, out int currentUserId) && currentUserId == id)
                {
                    // Update the current user's claims to reflect the changes
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                        new Claim(ClaimTypes.Name, user.UserName),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role ?? "Admin"),
                        new Claim("FirstName", user.FirstName ?? ""),
                        new Claim("LastName", user.LastName ?? ""),
                        new Claim("PicturePath", picturePath ?? "")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity)
                    );
                }
                
                return RedirectToAction(nameof(Index));
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to update admin user {UserId}: {Error}", id, errorContent);
            ModelState.AddModelError("", "Failed to update admin user");
            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin user {UserId}", id);
            ModelState.AddModelError("", "An error occurred");
            return View(user);
        }
    }

    // GET: AdminUserManagement/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            var response = await _httpClient.GetAsync($"{identityServiceUrl}/api/Users/{id}");
            
            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<AdminUserDto>>(json, new JsonSerializerOptions 
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
            _logger.LogError(ex, "Error loading admin user {UserId} for deletion", id);
            return NotFound();
        }
    }

    // POST: AdminUserManagement/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            AttachAuthToken();
            var identityServiceUrl = _configuration["ApiUrls:IdentityService"];
            var response = await _httpClient.DeleteAsync($"{identityServiceUrl}/api/Users/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            
            _logger.LogError("Failed to delete admin user {UserId}", id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting admin user {UserId}", id);
            return RedirectToAction(nameof(Index));
        }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }
}
