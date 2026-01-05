using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.Events;
using BusSystem.Menu.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BusSystem.Menu.API.EventHandlers;

/// <summary>
/// Handles the BusDeletedEvent to clean up categories and menu items
/// when a bus is deleted from the Bus Service
/// </summary>
public class BusDeletedEventHandler : IEventHandler<BusDeletedEvent>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BusDeletedEventHandler> _logger;

    public BusDeletedEventHandler(
        IServiceProvider serviceProvider,
        ILogger<BusDeletedEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Handle(BusDeletedEvent @event)
    {
        _logger.LogInformation("Handling BusDeletedEvent for bus {BusId}", @event.BusId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MenuDbContext>();

        try
        {
            // Find all categories for this bus
            var categories = await context.Categories
                .Where(c => c.BusId == @event.BusId)
                .ToListAsync();

            if (categories.Any())
            {
                // EF Core will cascade delete menu items automatically
                context.Categories.RemoveRange(categories);
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Deleted {CategoryCount} categories and their menu items for bus {BusId}",
                    categories.Count, @event.BusId);
            }
            else
            {
                _logger.LogInformation("No categories found for bus {BusId}", @event.BusId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling BusDeletedEvent for bus {BusId}", @event.BusId);
            // Don't throw - we don't want to break the event bus
        }
    }
}

