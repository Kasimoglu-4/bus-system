using BusSystem.EventBus.Events;

namespace BusSystem.EventBus.Abstractions;

/// <summary>
/// Interface for publishing and subscribing to integration events
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish an event to the message bus
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent;

    /// <summary>
    /// Subscribe to an event type
    /// </summary>
    void Subscribe<TEvent, TEventHandler>()
        where TEvent : IntegrationEvent
        where TEventHandler : IEventHandler<TEvent>;
}

/// <summary>
/// Interface for handling integration events
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task Handle(TEvent @event);
}

