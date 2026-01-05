namespace BusSystem.Menu.API.Models.DTOs;

// Category DTOs
public record CategoryCreateDto(int BusId, string Name);

public record CategoryUpdateDto(string? Name);

public record CategoryReadDto(int CategoryId, int BusId, string Name, int MenuItemCount);

public record CategoryWithItemsDto(
    int CategoryId,
    int BusId,
    string Name,
    List<MenuItemReadDto> MenuItems
);

// MenuItem DTOs
public record MenuItemCreateDto(
    int CategoryId,
    string Name,
    string? Description,
    string? Image,
    decimal Price
);

public record MenuItemUpdateDto(
    int? CategoryId,
    string? Name,
    string? Description,
    string? Image,
    decimal? Price
);

public record MenuItemReadDto(
    int MenuItemId,
    int CategoryId,
    string Name,
    string? Description,
    string? Image,
    decimal Price
);

// Bus Menu DTO (for passenger view)
public record BusMenuDto(
    int BusId,
    string PlateNumber,
    string? Description,
    List<CategoryWithItemsDto> Categories
);

