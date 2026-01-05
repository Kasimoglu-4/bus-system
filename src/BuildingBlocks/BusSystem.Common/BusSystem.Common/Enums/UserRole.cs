namespace BusSystem.Common.Enums;

/// <summary>
/// Defines the available user roles in the system
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Manager role - Can view and edit content but limited deletion rights
    /// </summary>
    Manager = 1,

    /// <summary>
    /// Admin role - Can manage buses and menus but cannot manage other users
    /// </summary>
    Admin = 2,

    /// <summary>
    /// SuperAdmin role - Full system access (can manage users, buses, menus, everything)
    /// </summary>
    SuperAdmin = 3
}

/// <summary>
/// Extension methods for UserRole enum
/// </summary>
public static class UserRoleExtensions
{
    /// <summary>
    /// Convert UserRole enum to string
    /// </summary>
    public static string ToRoleString(this UserRole role)
    {
        return role.ToString();
    }

    /// <summary>
    /// Parse string to UserRole enum
    /// </summary>
    public static UserRole ParseRole(string roleString)
    {
        if (Enum.TryParse<UserRole>(roleString, true, out var role))
        {
            return role;
        }
        
        // Default to Manager if parsing fails
        return UserRole.Manager;
    }

    /// <summary>
    /// Check if role string is valid
    /// </summary>
    public static bool IsValidRole(string roleString)
    {
        return Enum.TryParse<UserRole>(roleString, true, out _);
    }

    /// <summary>
    /// Get all role names as strings
    /// </summary>
    public static List<string> GetAllRoleNames()
    {
        return Enum.GetNames(typeof(UserRole)).ToList();
    }
}

