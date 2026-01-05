using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using BusSystem.Identity.API.Data;
using BusSystem.Identity.API.Models;
using BusSystem.Identity.API.Models.DTOs;
using BusSystem.Identity.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BusSystem.Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<AdminUser> _passwordHasher;

    public AuthController(
        IdentityDbContext context,
        IJwtService jwtService,
        IEmailService emailService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
        _passwordHasher = new PasswordHasher<AdminUser>();
    }

    /// <summary>
    /// Login with username and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 401)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse<LoginResponse>.FailureResponse("Username and password are required."));
        }

        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName);

        if (user == null)
        {
            _logger.LogWarning("Login attempt failed for user {UserName} - user not found", request.UserName);
            return Unauthorized(ApiResponse<LoginResponse>.FailureResponse("Invalid credentials."));
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login attempt failed for user {UserName} - invalid password", request.UserName);
            return Unauthorized(ApiResponse<LoginResponse>.FailureResponse("Invalid credentials."));
        }

        // Update last login date
        var userToUpdate = await _context.AdminUsers.FindAsync(user.Id);
        if (userToUpdate != null)
        {
            userToUpdate.LastLoginDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var token = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var loginResponse = new LoginResponse(
            Success: true,
            Token: token,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            User: new UserInfo(
                user.Id,
                user.UserName,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PicturePath,
                user.Role
            ),
            Message: "Login successful"
        );

        _logger.LogInformation("User {UserName} logged in successfully", user.UserName);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(loginResponse));
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserReadDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<UserReadDto>>> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            throw new UnauthorizedException();
        }

        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        var userDto = new UserReadDto(
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PicturePath,
            user.Role,
            user.CreatedDate,
            user.LastLoginDate
        );

        return Ok(ApiResponse<UserReadDto>.SuccessResponse(userDto));
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> UpdateProfile([FromBody] ProfileUpdateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            throw new UnauthorizedException();
        }

        var user = await _context.AdminUsers.FindAsync(int.Parse(userId));
        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated their profile", userId);
        return Ok(ApiResponse.SuccessResponse("Profile updated successfully"));
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            throw new UnauthorizedException();
        }

        var user = await _context.AdminUsers.FindAsync(int.Parse(userId));
        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, request.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new ValidationException("Current password is incorrect");
        }

        user.Password = _passwordHasher.HashPassword(user, request.NewPassword);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} changed their password", userId);
        return Ok(ApiResponse.SuccessResponse("Password changed successfully"));
    }

    /// <summary>
    /// Validate JWT token
    /// </summary>
    [HttpPost("validate-token")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public ActionResult<ApiResponse<bool>> ValidateToken([FromBody] string token)
    {
        var isValid = _jwtService.ValidateToken(token);
        return Ok(ApiResponse<bool>.SuccessResponse(isValid));
    }

    /// <summary>
    /// Request password reset - sends reset email
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PasswordResetResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PasswordResetResponse>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(ApiResponse<PasswordResetResponse>.FailureResponse("Email is required"));
            }

            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            // Always return success to prevent email enumeration attacks
            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
                return Ok(ApiResponse<PasswordResetResponse>.SuccessResponse(
                    new PasswordResetResponse(true, "If the email exists, a password reset link has been sent.")));
            }

            // Generate reset token
            var resetToken = GenerateSecureToken();
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour

            await _context.SaveChangesAsync();

            // Generate reset link
            var adminWebUrl = _configuration["AdminWebUrl"] ?? "http://localhost:5000";
            var resetLink = $"{adminWebUrl}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(resetToken)}";

            // Send email
            var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, user.UserName, resetLink);

            if (!emailSent)
            {
                _logger.LogError("Failed to send password reset email to {Email}", user.Email);
            }

            _logger.LogInformation("Password reset requested for user {UserId} ({Email})", user.Id, user.Email);

            return Ok(ApiResponse<PasswordResetResponse>.SuccessResponse(
                new PasswordResetResponse(true, "If the email exists, a password reset link has been sent.")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request");
            return StatusCode(500, ApiResponse<PasswordResetResponse>.FailureResponse("An error occurred processing your request"));
        }
    }

    /// <summary>
    /// Validate password reset token
    /// </summary>
    [HttpPost("validate-reset-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<ActionResult<ApiResponse<bool>>> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return Ok(ApiResponse<bool>.SuccessResponse(false));
        }

        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.PasswordResetToken == request.Token);

        if (user == null || user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            return Ok(ApiResponse<bool>.SuccessResponse(false));
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true));
    }

    /// <summary>
    /// Reset password with token
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PasswordResetResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PasswordResetResponse>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Token) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(ApiResponse<PasswordResetResponse>.FailureResponse("All fields are required"));
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(ApiResponse<PasswordResetResponse>.FailureResponse("Password must be at least 6 characters long"));
            }

            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.PasswordResetToken == request.Token);

            if (user == null)
            {
                return BadRequest(ApiResponse<PasswordResetResponse>.FailureResponse("Invalid or expired reset token"));
            }

            if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(ApiResponse<PasswordResetResponse>.FailureResponse("Reset token has expired"));
            }

            // Update password
            user.Password = _passwordHasher.HashPassword(user, request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password successfully reset for user {UserId} ({Email})", user.Id, user.Email);

            return Ok(ApiResponse<PasswordResetResponse>.SuccessResponse(
                new PasswordResetResponse(true, "Password has been reset successfully")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(500, ApiResponse<PasswordResetResponse>.FailureResponse("An error occurred resetting your password"));
        }
    }

    /// <summary>
    /// Generate a cryptographically secure random token
    /// </summary>
    private string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

