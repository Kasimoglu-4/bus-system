using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BusSystem.Admin.Web.Middleware;

public class ValidateUserExistsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidateUserExistsMiddleware> _logger;

    public ValidateUserExistsMiddleware(RequestDelegate next, ILogger<ValidateUserExistsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        // Skip validation for anonymous requests, static files, or specific paths
        if (!context.User.Identity?.IsAuthenticated == true ||
            context.Request.Path.StartsWithSegments("/Account/Login") ||
            context.Request.Path.StartsWithSegments("/Account/Logout") ||
            context.Request.Path.StartsWithSegments("/css") ||
            context.Request.Path.StartsWithSegments("/js") ||
            context.Request.Path.StartsWithSegments("/lib") ||
            context.Request.Path.StartsWithSegments("/uploads") ||
            context.Request.Path.StartsWithSegments("/favicon.ico"))
        {
            await _next(context);
            return;
        }

        try
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Authenticated user has no NameIdentifier claim");
                await LogoutUser(context, "session");
                return;
            }

            // Check if JWT token exists in session
            var token = context.Session.GetString("jwt");
            
            if (string.IsNullOrEmpty(token))
            {
                // Session expired - JWT token is gone or when i close the app then open again
                _logger.LogInformation("Session expired for user {UserId}. No JWT token found.", userId);
                await LogoutUser(context, "session");
                return;
            }

            // Check if user still exists in the database
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var identityServiceUrl = configuration["ApiUrls:IdentityService"];
            var response = await httpClient.GetAsync($"{identityServiceUrl}/api/Users/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                // User doesn't exist or was deleted - log them out
                _logger.LogWarning("User {UserId} no longer exists or was deleted. Logging out.", userId);
                await LogoutUser(context, "deleted");
                return;
            }

            // User exists, check if claims need to be refreshed
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Data != null)
            {
                // Check if the PicturePath has changed
                var currentPicturePath = context.User.FindFirst("PicturePath")?.Value ?? "";
                var newPicturePath = apiResponse.Data.PicturePath ?? "";

                if (currentPicturePath != newPicturePath)
                {
                    // Update claims with fresh data
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, apiResponse.Data.Id.ToString()),
                        new Claim(ClaimTypes.Name, apiResponse.Data.UserName),
                        new Claim(ClaimTypes.Email, apiResponse.Data.Email ?? ""),
                        new Claim(ClaimTypes.Role, apiResponse.Data.Role ?? "Admin"),
                        new Claim("FirstName", apiResponse.Data.FirstName ?? ""),
                        new Claim("LastName", apiResponse.Data.LastName ?? ""),
                        new Claim("PicturePath", newPicturePath)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await context.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity)
                    );

                    _logger.LogInformation("Refreshed claims for user {UserId} - PicturePath updated", userId);
                }
            }

            // User exists, continue with the request
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user existence");
            // In case of error, allow the request to continue
            // You might want to change this based on your security requirements
            await _next(context);
        }
    }

    private async Task LogoutUser(HttpContext context, string reason)
    {
        // Clear the session
        context.Session.Clear();
        
        // Sign out the user
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Redirect to login page with appropriate message
        var message = reason switch
        {
            "session" => "Your session has expired. Please log in again.",
            "deleted" => "Your account has been deleted or is no longer valid.",
            _ => "Please log in to continue."
        };
        
        var messageType = reason == "deleted" ? "error" : "info";
        
        context.Response.Redirect($"/Account/Login?message={Uri.EscapeDataString(message)}&type={messageType}");
    }

    // Response models
    private class ApiResponse
    {
        public bool Success { get; set; }
        public UserData? Data { get; set; }
    }

    private class UserData
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public string? PicturePath { get; set; }
    }
}

