using BusSystem.Identity.API.Models;

namespace BusSystem.Identity.API.Services;

public interface IJwtService
{
    string GenerateAccessToken(AdminUser user);
    string GenerateRefreshToken();
    bool ValidateToken(string token);
}

