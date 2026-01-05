namespace BusSystem.Identity.API.Models.DTOs;

public record LoginRequest(string UserName, string Password);

public record LoginResponse(
    bool Success,
    string? Token,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserInfo? User,
    string? Message
);

public record UserInfo(
    int Id,
    string UserName,
    string Email,
    string? FirstName,
    string? LastName,
    string? PicturePath,
    string Role
);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ProfileUpdateRequest(
    string? FirstName,
    string? LastName,
    string? Email
);

public record UserCreateRequest(
    string UserName,
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    string? PicturePath,
    string Role
);

public record UserUpdateRequest(
    string? UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Role,
    string? PicturePath,
    string? CurrentPassword,
    string? NewPassword
);

public record UserReadDto(
    int Id,
    string UserName,
    string Email,
    string? FirstName,
    string? LastName,
    string? PicturePath,
    string Role,
    DateTime CreatedDate,
    DateTime? LastLoginDate
);

// Password Recovery DTOs
public record ForgotPasswordRequest(string Email);

public record ValidateResetTokenRequest(string Email, string Token);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword
);

public record PasswordResetResponse(
    bool Success,
    string Message
);

