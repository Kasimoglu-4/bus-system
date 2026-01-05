using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using BusSystem.Common.Authorization;
using BusSystem.Menu.API.Data;
using BusSystem.Menu.API.Models;
using BusSystem.Menu.API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusSystem.Menu.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuItemsController : ControllerBase
{
    private readonly MenuDbContext _context;
    private readonly ILogger<MenuItemsController> _logger;

    public MenuItemsController(
        MenuDbContext context,
        ILogger<MenuItemsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all menu items (optionally filtered by CategoryId)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemReadDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<MenuItemReadDto>>>> GetMenuItems(
        [FromQuery] int? categoryId)
    {
        var query = _context.MenuItems.AsNoTracking();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        var menuItems = await query
            .Select(m => new MenuItemReadDto(
                m.MenuItemId,
                m.CategoryId,
                m.Name,
                m.Description,
                m.Image,
                m.Price
            ))
            .ToListAsync();

        return Ok(ApiResponse<List<MenuItemReadDto>>.SuccessResponse(menuItems));
    }

    /// <summary>
    /// Get menu item by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemReadDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<MenuItemReadDto>>> GetMenuItem(int id)
    {
        var menuItem = await _context.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MenuItemId == id);

        if (menuItem == null)
        {
            throw new NotFoundException("MenuItem", id);
        }

        var menuItemDto = new MenuItemReadDto(
            menuItem.MenuItemId,
            menuItem.CategoryId,
            menuItem.Name,
            menuItem.Description,
            menuItem.Image,
            menuItem.Price
        );

        return Ok(ApiResponse<MenuItemReadDto>.SuccessResponse(menuItemDto));
    }

    /// <summary>
    /// Create new menu item (SuperAdmin and Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse<MenuItemReadDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<MenuItemReadDto>>> CreateMenuItem([FromBody] MenuItemCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ValidationException("Menu item name is required");
        }

        if (dto.Price < 0)
        {
            throw new ValidationException("Price cannot be negative");
        }

        // Verify category exists
        var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId);
        if (!categoryExists)
        {
            throw new ValidationException($"Category with ID {dto.CategoryId} does not exist");
        }

        var menuItem = new MenuItem
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Image = dto.Image,
            Price = dto.Price
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        var menuItemDto = new MenuItemReadDto(
            menuItem.MenuItemId,
            menuItem.CategoryId,
            menuItem.Name,
            menuItem.Description,
            menuItem.Image,
            menuItem.Price
        );

        _logger.LogInformation("MenuItem {MenuItemId} created in category {CategoryId}",
            menuItem.MenuItemId, menuItem.CategoryId);

        return CreatedAtAction(
            nameof(GetMenuItem),
            new { id = menuItem.MenuItemId },
            ApiResponse<MenuItemReadDto>.SuccessResponse(menuItemDto, "Menu item created successfully")
        );
    }

    /// <summary>
    /// Update menu item (SuperAdmin, Admin, and Manager)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.ContentEditing)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> UpdateMenuItem(int id, [FromBody] MenuItemUpdateDto dto)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            throw new NotFoundException("MenuItem", id);
        }

        // Update CategoryId if provided and verify it exists
        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId.Value);
            if (!categoryExists)
            {
                throw new ValidationException($"Category with ID {dto.CategoryId.Value} does not exist");
            }
            menuItem.CategoryId = dto.CategoryId.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            menuItem.Name = dto.Name.Trim();
        }

        if (dto.Description != null)
        {
            menuItem.Description = dto.Description.Trim();
        }

        // Always update Image field - allows setting to null to remove image
        menuItem.Image = dto.Image;

        if (dto.Price.HasValue)
        {
            if (dto.Price.Value < 0)
            {
                throw new ValidationException("Price cannot be negative");
            }
            menuItem.Price = dto.Price.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("MenuItem {MenuItemId} updated", id);

        return Ok(ApiResponse.SuccessResponse("Menu item updated successfully"));
    }

    /// <summary>
    /// Delete menu item (SuperAdmin and Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> DeleteMenuItem(int id)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            throw new NotFoundException("MenuItem", id);
        }

        var categoryId = menuItem.CategoryId;

        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("MenuItem {MenuItemId} deleted", id);

        return Ok(ApiResponse.SuccessResponse("Menu item deleted successfully"));
    }

}

