using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using BusSystem.Admin.Web.Models.ViewModels;

namespace BusSystem.Admin.Web.Controllers;

public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AccountController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string userName, string password, string? returnUrl = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("jwt");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            // Call Identity.API login endpoint
            var loginRequest = new
            {
                userName = userName,
                password = password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync($"{identityApiUrl}/api/Auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var loginApiResponse = JsonSerializer.Deserialize<LoginApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var loginData = loginApiResponse?.Data;
            var userResponse = loginData?.User;
            if (loginApiResponse == null || !loginApiResponse.Success || loginData?.Token == null || userResponse == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid response from server");
                return View();
            }

            // store jwt in session
            HttpContext.Session.SetString("jwt", loginData.Token);

            // Create claims and sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userResponse.Id.ToString()),
                new Claim(ClaimTypes.Name, userResponse.UserName),
                new Claim(ClaimTypes.Email, userResponse.Email ?? ""),
                new Claim(ClaimTypes.Role, userResponse.Role ?? "Admin"),
                new Claim("FirstName", userResponse.FirstName ?? ""),
                new Claim("LastName", userResponse.LastName ?? ""),
                new Claim("PicturePath", userResponse.PicturePath ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity)
            );

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return Redirect("/AdminPanel/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"An error occurred: {ex.Message}");
            return View();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null) return RedirectToAction("Login");

        try
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("jwt");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            var response = await client.GetAsync($"{identityApiUrl}/api/Users/{userIdStr}");

            if (!response.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserResponse>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Data == null) return NotFound();

            var user = apiResponse.Data;

            var vm = new ProfileEditViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email,
                UserName = user.UserName,
                ExistingPicturePath = user.PicturePath
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error loading profile: {ex.Message}";
            return RedirectToAction("Index", "AdminPanel");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var token = HttpContext.Session.GetString("jwt");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            // Handle profile picture upload locally
            string? picturePath = model.ExistingPicturePath;
            if (model.ProfilePicture != null && model.ProfilePicture.Length > 0)
            {
                var uploadsFolder = Path.Combine("wwwroot", "uploads", "users");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"user_{model.Id}_{Guid.NewGuid()}{Path.GetExtension(model.ProfilePicture.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await model.ProfilePicture.CopyToAsync(stream);
                }

                picturePath = $"/uploads/users/{fileName}";
            }

            // Prepare update request
            var updateRequest = new
            {
                id = model.Id,
                firstName = model.FirstName,
                lastName = model.LastName,
                email = model.Email,
                userName = model.UserName,
                picturePath = picturePath,
                currentPassword = model.CurrentPassword,
                newPassword = model.NewPassword
            };

            var content = new StringContent(
                JsonSerializer.Serialize(updateRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PutAsync($"{identityApiUrl}/api/Users/{model.Id}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (errorContent.Contains("Current password is incorrect"))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect");
                    return View(model);
                }

                TempData["Error"] = "Failed to update profile";
                return View(model);
            }

            // Update claims with new information
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, model.Id.ToString()),
                new Claim(ClaimTypes.Name, model.UserName),
                new Claim(ClaimTypes.Email, model.Email),
                new Claim(ClaimTypes.Role, User.FindFirstValue(ClaimTypes.Role) ?? "Admin"),
                new Claim("FirstName", model.FirstName),
                new Claim("LastName", model.LastName),
                new Claim("PicturePath", picturePath ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity)
            );

            TempData["Success"] = "Profile updated successfully";
            return RedirectToAction("Profile");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error updating profile: {ex.Message}";
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // GET: /Account/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // POST: /Account/ForgotPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            var forgotPasswordRequest = new
            {
                email = email
            };

            var content = new StringContent(
                JsonSerializer.Serialize(forgotPasswordRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync($"{identityApiUrl}/api/Auth/forgot-password", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "If the email exists, a password reset link has been sent to your email address.";
            }
            else
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again later.";
            }

            return View();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return View();
        }
    }

    // GET: /Account/ResetPassword
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "Invalid password reset link.";
            return View();
        }

        try
        {
            // Validate the token before showing the form
            var client = _httpClientFactory.CreateClient();
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            var validateRequest = new
            {
                email = email,
                token = token
            };

            var content = new StringContent(
                JsonSerializer.Serialize(validateRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync($"{identityApiUrl}/api/Auth/validate-reset-token", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link.";
                return View();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Data != true)
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link.";
            }

            return View();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return View();
        }
    }

    // POST: /Account/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "Passwords do not match.";
            return View();
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var identityApiUrl = _configuration["ApiUrls:IdentityService"] ?? "http://localhost:5271";

            var resetPasswordRequest = new
            {
                email = email,
                token = token,
                newPassword = newPassword
            };

            var content = new StringContent(
                JsonSerializer.Serialize(resetPasswordRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync($"{identityApiUrl}/api/Auth/reset-password", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Your password has been reset successfully. You can now log in with your new password.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                TempData["ErrorMessage"] = apiResponse?.Message ?? "Failed to reset password. The link may have expired.";
            }

            return View();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return View();
        }
    }

    // Response models
    private class LoginApiResponse
    {
        public bool Success { get; set; }
        public LoginData? Data { get; set; }
        public string? Message { get; set; }
    }

    private class LoginData
    {
        public string? Token { get; set; }
        public UserInfo? User { get; set; }
    }

    private class UserInfo
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PicturePath { get; set; }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    private class UserResponse
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public string? PicturePath { get; set; }
    }
}
