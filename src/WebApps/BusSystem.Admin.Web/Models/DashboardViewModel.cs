namespace BusSystem.Admin.Web.Models;

public class DashboardViewModel
{
    public int TotalBuses { get; set; }
    public int TotalCategories { get; set; }
    public int TotalMenuItems { get; set; }
    public int InactiveBuses { get; set; }
    public List<BusDto> RecentBuses { get; set; } = new();
    public List<MenuItemDto> RecentMenuItems { get; set; } = new();
}

public class BusDto
{
    public int BusId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? QRCodeUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? Description { get; set; }
    public int CreatedBy { get; set; }
}

public class MenuItemDto
{
    public int MenuItemId { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public decimal Price { get; set; }
    public string? CategoryName { get; set; }
    public string? BusPlateNumber { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int BusId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MenuItemCount { get; set; }
}

public class AdminUserDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Picture { get; set; }
    public string Role { get; set; } = "Admin";
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

public class AdminUserEditDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PicturePath { get; set; }
    public string Role { get; set; } = "Admin";
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}
