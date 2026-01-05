using Microsoft.AspNetCore.Mvc;
using BusSystem.Web.Models.ViewModels;
using System.Text.Json;

namespace BusSystem.Web.Controllers;

public class BusViewController : Controller
{
    private readonly ILogger<BusViewController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public BusViewController(
        ILogger<BusViewController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
    }

    // Handle route: /PlateNumber/{plateNumber}
    [HttpGet("/PlateNumber/{plateNumber}")]
    public async Task<IActionResult> Index(string plateNumber)
    {
        _logger.LogInformation("Accessed bus with plate number {PlateNumber}", plateNumber);
        
        try
        {
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];

            // Get bus by plate number
            var busResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses/plate/{plateNumber}");
            
            if (!busResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Bus not found with plate number {PlateNumber}", plateNumber);
                return NotFound($"Bus with plate number '{plateNumber}' not found");
            }

            var busJson = await busResponse.Content.ReadAsStringAsync();
            var busApiResponse = JsonSerializer.Deserialize<ApiResponse<BusDto>>(busJson, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (busApiResponse?.Data == null)
            {
                return NotFound($"Bus with plate number '{plateNumber}' not found");
            }

            var bus = busApiResponse.Data;

            // Get categories with menu items for this bus
            var categoriesResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories/bus/{bus.BusId}/with-items");
            
            if (!categoriesResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch categories for bus {BusId}", bus.BusId);
                return View(new BusMenuViewModel 
                { 
                    BusId = bus.BusId,
                    PlateNumber = bus.PlateNumber,
                    Description = bus.Description,
                    CreatedDate = bus.CreatedDate,
                    Categories = new List<CategoryMenuViewModel>()
                });
            }

            var categoriesJson = await categoriesResponse.Content.ReadAsStringAsync();
            var categoriesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryWithItemsDto>>>(categoriesJson, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var categories = categoriesApiResponse?.Data ?? new List<CategoryWithItemsDto>();

            // Map to view model
            var viewModel = new BusMenuViewModel
            {
                BusId = bus.BusId,
                PlateNumber = bus.PlateNumber,
                Description = bus.Description,
                CreatedDate = bus.CreatedDate,
                Categories = categories.Select(c => new CategoryMenuViewModel
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    MenuItems = c.MenuItems.Select(m => new MenuItemViewModel
                    {
                        MenuItemId = m.MenuItemId,
                        Name = m.Name,
                        Description = m.Description,
                        Image = m.Image,
                        Price = m.Price
                    }).ToList()
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bus menu for plate number {PlateNumber}", plateNumber);
            return StatusCode(500, "An error occurred while loading the bus menu");
        }
    }

    // DTOs for API responses
    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    private class BusDto
    {
        public int BusId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string? QRCodeUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Description { get; set; }
        public int CreatedBy { get; set; }
    }

    private class CategoryWithItemsDto
    {
        public int CategoryId { get; set; }
        public int BusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<MenuItemDto> MenuItems { get; set; } = new();
    }

    private class MenuItemDto
    {
        public int MenuItemId { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Image { get; set; }
        public decimal Price { get; set; }
    }
}
