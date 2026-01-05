namespace BusSystem.Common.Authorization;

/// <summary>
/// Constants for role-based authorization
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";

    /// <summary>
    /// Roles that can manage users (create, edit, delete)
    /// </summary>
    public const string UserManagement = SuperAdmin;

    /// <summary>
    /// Roles that can create and delete buses/menus
    /// </summary>
    public const string ContentCreation = SuperAdmin + "," + Admin;

    /// <summary>
    /// Roles that can edit content
    /// </summary>
    public const string ContentEditing = SuperAdmin + "," + Admin + "," + Manager;

    /// <summary>
    /// Roles that can view content (all authenticated users)
    /// </summary>
    public const string ContentViewing = SuperAdmin + "," + Admin + "," + Manager;
}

