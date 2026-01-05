namespace BusSystem.EventBus.Events;

/// <summary>
/// Base class for all integration events used for inter-service communication
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; }
    public DateTime OccurredOn { get; init; }

    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}

/// <summary>
/// Event published when a bus is created
/// </summary>
public record BusCreatedEvent(int BusId, string PlateNumber, string? Description) : IntegrationEvent;

/// <summary>
/// Event published when a bus is updated
/// </summary>
public record BusUpdatedEvent(int BusId, string PlateNumber, string? Description) : IntegrationEvent;

/// <summary>
/// Event published when a bus is deleted
/// </summary>
public record BusDeletedEvent(int BusId) : IntegrationEvent;

/// <summary>
/// Event published when a category is deleted
/// </summary>
public record CategoryDeletedEvent(int CategoryId, int BusId) : IntegrationEvent;

