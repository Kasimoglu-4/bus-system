using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using BusSystem.Common.Authorization;
using BusSystem.Identity.API.Data;
using BusSystem.Identity.API.Models;
using BusSystem.Identity.API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusSystem.Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // All endpoints require authentication by default
public class UsersController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly ILogger<UsersController> _logger;
    private readonly PasswordHasher<AdminUser> _passwordHasher;

    public UsersController(IdentityDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
        _passwordHasher = new PasswordHasher<AdminUser>();
    }

    /// <summary>
    /// Get all users (SuperAdmin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.UserManagement)]
    [ProducesResponseType(typeof(ApiResponse<List<UserReadDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<UserReadDto>>>> GetUsers()
    {
        var users = await _context.AdminUsers
            .AsNoTracking()
            .Select(u => new UserReadDto(
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PicturePath,
                u.Role,
                u.CreatedDate,
                u.LastLoginDate
            ))
            .ToListAsync();

        return Ok(ApiResponse<List<UserReadDto>>.SuccessResponse(users));
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserReadDto>), 200)]
    [ProducesResponseType(404)]
    [AllowAnonymous] // Allow authenticated users to access their own profile
    public async Task<ActionResult<ApiResponse<UserReadDto>>> GetUser(int id)
    {
        // Check if user is authenticated
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        // Get the authenticated user's ID from claims
        var authenticatedUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        // Allow access if user is viewing their own profile OR has SuperAdmin role
        var isOwnProfile = authenticatedUserId == id.ToString();
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        
        if (!isOwnProfile && !isSuperAdmin)
        {
            return Forbid();
        }

        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            throw new NotFoundException("User", id);
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
    /// Create new user (SuperAdmin only, or first user if database is empty)
    /// </summary>
    [HttpPost]
    [AllowAnonymous] // Allow first user creation
    [ProducesResponseType(typeof(ApiResponse<UserReadDto>), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ApiResponse<UserReadDto>>> CreateUser([FromBody] UserCreateRequest request)
    {
        // Check if any users exist - only allow anonymous creation if database is empty
        var userCount = await _context.AdminUsers.CountAsync();
        if (userCount > 0)
        {
            // If users exist, require SuperAdmin role
            if (!User.Identity?.IsAuthenticated == true)
            {
                throw new UnauthorizedException("User creation requires authentication");
            }
            
            if (!User.IsInRole(Roles.SuperAdmin))
            {
                throw new UnauthorizedException("Only SuperAdmin can create new users");
            }
        }

        // Check if username already exists
        var existingUser = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName);
        
        if (existingUser != null)
        {
            throw new ValidationException("Username already exists");
        }

        // Check if email already exists
        var existingEmail = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (existingEmail != null)
        {
            throw new ValidationException("Email already exists");
        }

        var user = new AdminUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            PicturePath = request.PicturePath,
            Role = request.Role,
            CreatedDate = DateTime.UtcNow
        };

        user.Password = _passwordHasher.HashPassword(user, request.Password);

        _context.AdminUsers.Add(user);
        await _context.SaveChangesAsync();

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

        _logger.LogInformation("New user created with ID {UserId}", user.Id);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ApiResponse<UserReadDto>.SuccessResponse(userDto));
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [AllowAnonymous] // Allow authenticated users to update their own profile
    public async Task<ActionResult<ApiResponse>> UpdateUser(int id, [FromBody] UserUpdateRequest request)
    {
        // Check if user is authenticated
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        // Get the authenticated user's ID from claims
        var authenticatedUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        // Allow access if user is updating their own profile OR has SuperAdmin role
        var isOwnProfile = authenticatedUserId == id.ToString();
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        
        if (!isOwnProfile && !isSuperAdmin)
        {
            return Forbid();
        }

        var user = await _context.AdminUsers.FindAsync(id);
        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        // Update basic info
        if (!string.IsNullOrWhiteSpace(request.UserName))
            user.UserName = request.UserName.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName.Trim();
        
        // Update picture path
        if (!string.IsNullOrWhiteSpace(request.PicturePath))
            user.PicturePath = request.PicturePath;
        
        // Only SuperAdmins can change roles
        if (!string.IsNullOrWhiteSpace(request.Role) && isSuperAdmin)
            user.Role = request.Role;

        // Handle password change
        if (!string.IsNullOrWhiteSpace(request.CurrentPassword) && !string.IsNullOrWhiteSpace(request.NewPassword))
        {
            // Verify current password
            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.Password, request.CurrentPassword);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return BadRequest(ApiResponse.FailureResponse("Current password is incorrect"));
            }

            // Hash and update new password
            user.Password = _passwordHasher.HashPassword(user, request.NewPassword);
            _logger.LogInformation("Password updated for user {UserId}", id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated", id);
        return Ok(ApiResponse.SuccessResponse("User updated successfully"));
    }

    /// <summary>
    /// Delete user (SuperAdmin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.UserManagement)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse>> DeleteUser(int id)
    {
        var user = await _context.AdminUsers.FindAsync(id);
        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        _context.AdminUsers.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted", id);
        return Ok(ApiResponse.SuccessResponse("User deleted successfully"));
    }
}

