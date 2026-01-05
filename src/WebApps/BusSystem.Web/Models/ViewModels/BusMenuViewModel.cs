namespace BusSystem.Web.Models.ViewModels
{
    public class BusMenuViewModel
    {
        public int BusId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? Description { get; set; }
        public List<CategoryMenuViewModel> Categories { get; set; } = new();
    }

    public class CategoryMenuViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Image { get; set; }
        public List<MenuItemViewModel> MenuItems { get; set; } = new();
    }

    public class MenuItemViewModel
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Image {get; set; }
        public decimal Price { get; set; }
    }
}
