using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BusSystem.Admin.Web.Models;
using System.Text.Json;

namespace BusSystem.Admin.Web.Controllers;

[Authorize]
public class AdminPanelController : Controller
{
    private readonly ILogger<AdminPanelController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AdminPanelController(
        ILogger<AdminPanelController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var busServiceUrl = _configuration["ApiUrls:BusService"];
            var menuServiceUrl = _configuration["ApiUrls:MenuService"];

            var viewModel = new DashboardViewModel();
            var buses = new List<BusDto>();
            var categories = new List<CategoryDto>();

            // Get all buses
            var busesResponse = await _httpClient.GetAsync($"{busServiceUrl}/api/buses");
            if (busesResponse.IsSuccessStatusCode)
            {
                var busesJson = await busesResponse.Content.ReadAsStringAsync();
                var busesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<BusDto>>>(busesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                buses = busesApiResponse?.Data ?? new List<BusDto>();
                viewModel.TotalBuses = buses.Count;
                viewModel.RecentBuses = buses.OrderByDescending(b => b.CreatedDate).Take(5).ToList();
            }

            // Get all categories
            var categoriesResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/categories");
            if (categoriesResponse.IsSuccessStatusCode)
            {
                var categoriesJson = await categoriesResponse.Content.ReadAsStringAsync();
                var categoriesApiResponse = JsonSerializer.Deserialize<ApiResponse<List<CategoryDto>>>(categoriesJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                categories = categoriesApiResponse?.Data ?? new List<CategoryDto>();
                viewModel.TotalCategories = categories.Count;
            }

            // Get all menu items
            var menuItemsResponse = await _httpClient.GetAsync($"{menuServiceUrl}/api/menuitems");
            if (menuItemsResponse.IsSuccessStatusCode)
            {
                var menuItemsJson = await menuItemsResponse.Content.ReadAsStringAsync();
                var menuItemsApiResponse = JsonSerializer.Deserialize<ApiResponse<List<MenuItemDto>>>(menuItemsJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var menuItems = menuItemsApiResponse?.Data ?? new List<MenuItemDto>();
                viewModel.TotalMenuItems = menuItems.Count;
                viewModel.RecentMenuItems = menuItems.OrderByDescending(m => m.MenuItemId).Take(5).ToList();
            }

            // Calculate inactive buses (buses with no categories)
            var busIdsWithCategories = categories.Select(c => c.BusId).Distinct().ToHashSet();
            viewModel.InactiveBuses = buses.Count(b => !busIdsWithCategories.Contains(b.BusId));

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            return View(new DashboardViewModel());
        }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }
}
