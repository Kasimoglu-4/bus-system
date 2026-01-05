using System.Collections.Concurrent;
using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusSystem.EventBus.InMemory;

/// <summary>
/// Simple in-memory event bus implementation for development and testing
/// Replace with RabbitMQ or Azure Service Bus for production
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Type>> _handlers;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, List<Type>>();
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
    {
        var eventName = typeof(TEvent).Name;
        _logger.LogInformation("Publishing event {EventName} with Id {EventId}", eventName, @event.Id);

        if (_handlers.TryGetValue(eventName, out var handlerTypes))
        {
            using var scope = _serviceProvider.CreateScope();
            
            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    var handler = scope.ServiceProvider.GetService(handlerType);
                    if (handler != null)
                    {
                        var method = handlerType.GetMethod(nameof(IEventHandler<TEvent>.Handle));
                        if (method != null)
                        {
                            await (Task)method.Invoke(handler, new object[] { @event })!;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventName} with handler {HandlerType}", 
                        eventName, handlerType.Name);
                }
            }
        }
        else
        {
            _logger.LogDebug("No handlers registered for event {EventName}", eventName);
        }
    }

    public void Subscribe<TEvent, TEventHandler>()
        where TEvent : IntegrationEvent
        where TEventHandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var handlerType = typeof(TEventHandler);

        _handlers.AddOrUpdate(eventName,
            new List<Type> { handlerType },
            (key, existingHandlers) =>
            {
                if (!existingHandlers.Contains(handlerType))
                {
                    existingHandlers.Add(handlerType);
                }
                return existingHandlers;
            });

        _logger.LogInformation("Subscribed {HandlerType} to event {EventName}", handlerType.Name, eventName);
    }
}

