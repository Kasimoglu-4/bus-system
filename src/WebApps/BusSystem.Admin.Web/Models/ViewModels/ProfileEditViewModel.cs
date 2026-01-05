using System.ComponentModel.DataAnnotations;

namespace BusSystem.Admin.Web.Models.ViewModels;

public class ProfileEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "First name")]
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last name")]
    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "E-mail address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "User name")]
    public string UserName { get; set; } = string.Empty;

    [Display(Name = "Current password")]
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Display(Name = "New password")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "Confirm password")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? ProfilePicture { get; set; }

    public string? ExistingPicturePath { get; set; }
}
