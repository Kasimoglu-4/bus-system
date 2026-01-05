using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusSystem.Common.Enums;

namespace BusSystem.Identity.API.Models;

[Table("AdminUser")]
public class AdminUser
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(100), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    public string? PicturePath { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty; // hashed value

    [Required, StringLength(20)]
    public string Role { get; set; } = UserRole.Admin.ToString();

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }

    // Password Reset fields
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    /// <summary>
    /// Get the UserRole enum from the Role string
    /// </summary>
    [NotMapped]
    public UserRole UserRoleEnum => UserRoleExtensions.ParseRole(Role);
}

