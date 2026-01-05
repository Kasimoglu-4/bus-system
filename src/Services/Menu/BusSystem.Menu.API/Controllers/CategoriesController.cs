using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using BusSystem.Common.Authorization;
using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.Events;
using BusSystem.Menu.API.Data;
using BusSystem.Menu.API.Models;
using BusSystem.Menu.API.Models.DTOs;
using BusSystem.Menu.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusSystem.Menu.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly MenuDbContext _context;
    private readonly IBusServiceClient _busServiceClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(
        MenuDbContext context,
        IBusServiceClient busServiceClient,
        IEventBus eventBus,
        ILogger<CategoriesController> logger)
    {
        _context = context;
        _busServiceClient = busServiceClient;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Get all categories (optionally filtered by BusId)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CategoryReadDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<CategoryReadDto>>>> GetCategories(
        [FromQuery] int? busId)
    {
        var query = _context.Categories.AsNoTracking();

        if (busId.HasValue)
        {
            query = query.Where(c => c.BusId == busId.Value);
        }

        var categories = await query
            .Select(c => new CategoryReadDto(
                c.CategoryId,
                c.BusId,
                c.Name,
                c.MenuItems.Count
            ))
            .ToListAsync();

        return Ok(ApiResponse<List<CategoryReadDto>>.SuccessResponse(categories));
    }

    /// <summary>
    /// Get category by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryReadDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<CategoryReadDto>>> GetCategory(int id)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(c => new CategoryReadDto(
                c.CategoryId,
                c.BusId,
                c.Name,
                c.MenuItems.Count
            ))
            .FirstOrDefaultAsync();

        if (category == null)
        {
            throw new NotFoundException("Category", id);
        }

        return Ok(ApiResponse<CategoryReadDto>.SuccessResponse(category));
    }

    /// <summary>
    /// Get category with all menu items
    /// </summary>
    [HttpGet("{id}/with-items")]
    [ProducesResponseType(typeof(ApiResponse<CategoryWithItemsDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApiResponse<CategoryWithItemsDto>>> GetCategoryWithItems(int id)
    {
        var category = await _context.Categories
            .Include(c => c.MenuItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null)
        {
            throw new NotFoundException("Category", id);
        }

        var categoryDto = new CategoryWithItemsDto(
            category.CategoryId,
            category.BusId,
            category.Name,
            category.MenuItems.Select(m => new MenuItemReadDto(
                m.MenuItemId,
                m.CategoryId,
                m.Name,
                m.Description,
                m.Image,
                m.Price
            )).ToList()
        );

        return Ok(ApiResponse<CategoryWithItemsDto>.SuccessResponse(categoryDto));
    }

    /// <summary>
    /// Get all categories for a specific bus with menu items
    /// </summary>
    [HttpGet("bus/{busId}/with-items")]
    [ProducesResponseType(typeof(ApiResponse<List<CategoryWithItemsDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<CategoryWithItemsDto>>>> GetBusCategoriesWithItems(int busId)
    {
        var categories = await _context.Categories
            .Include(c => c.MenuItems)
            .Where(c => c.BusId == busId)
            .AsNoTracking()
            .ToListAsync();

        var categoryDtos = categories.Select(c => new CategoryWithItemsDto(
            c.CategoryId,
            c.BusId,
            c.Name,
            c.MenuItems.Select(m => new MenuItemReadDto(
                m.MenuItemId,
                m.CategoryId,
                m.Name,
                m.Description,
                m.Image,
                m.Price
            )).ToList()
        )).ToList();

        return Ok(ApiResponse<List<CategoryWithItemsDto>>.SuccessResponse(categoryDtos));
    }

    /// <summary>
    /// Create new category (SuperAdmin and Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse<CategoryReadDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse<CategoryReadDto>>> CreateCategory([FromBody] CategoryCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ValidationException("Category name is required");
        }

        // Verify bus exists
        var busExists = await _busServiceClient.BusExistsAsync(dto.BusId);
        if (!busExists)
        {
            throw new ValidationException($"Bus with ID {dto.BusId} does not exist");
        }

        var category = new Category
        {
            BusId = dto.BusId,
            Name = dto.Name.Trim()
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var categoryDto = new CategoryReadDto(
            category.CategoryId,
            category.BusId,
            category.Name,
            0
        );

        _logger.LogInformation("Category {CategoryId} created for bus {BusId}", 
            category.CategoryId, category.BusId);

        return CreatedAtAction(
            nameof(GetCategory),
            new { id = category.CategoryId },
            ApiResponse<CategoryReadDto>.SuccessResponse(categoryDto, "Category created successfully")
        );
    }

    /// <summary>
    /// Update category (SuperAdmin, Admin, and Manager)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.ContentEditing)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> UpdateCategory(int id, [FromBody] CategoryUpdateDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            throw new NotFoundException("Category", id);
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            category.Name = dto.Name.Trim();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Category {CategoryId} updated", id);

        return Ok(ApiResponse.SuccessResponse("Category updated successfully"));
    }

    /// <summary>
    /// Delete category (SuperAdmin and Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.ContentCreation)]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<ApiResponse>> DeleteCategory(int id)
    {
        var category = await _context.Categories
            .Include(c => c.MenuItems)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null)
        {
            throw new NotFoundException("Category", id);
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        // Publish event
        var categoryDeletedEvent = new CategoryDeletedEvent(category.CategoryId, category.BusId);
        await _eventBus.PublishAsync(categoryDeletedEvent);

        _logger.LogInformation("Category {CategoryId} deleted with {ItemCount} menu items",
            id, category.MenuItems.Count);

        return Ok(ApiResponse.SuccessResponse("Category deleted successfully"));
    }
}

