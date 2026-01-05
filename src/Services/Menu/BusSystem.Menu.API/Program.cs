using BusSystem.Common.Extensions;
using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.Events;
using BusSystem.EventBus.InMemory;
using BusSystem.Menu.API.Data;
using BusSystem.Menu.API.EventHandlers;
using BusSystem.Menu.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogLogging("MenuService");

try
{
    Log.Information("Starting Menu Service");

    // Add services to the container
    builder.Services.AddControllers();

// Swagger/OpenAPI configuration
builder.Services.AddSwaggerWithJwtAuth(
    title: "BusSystem Menu Service API",
    description: "Menu and category management service for Bus System microservices");

// Database configuration
builder.Services.AddSqlServerWithRetry<MenuDbContext>(builder.Configuration);

// JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// HttpClient for Bus Service with Polly retry policy
builder.Services.AddHttpClient<IBusServiceClient, BusServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:BusService"] ?? "http://localhost:5002");
})
.AddPolicyHandler(ServiceCollectionExtensions.GetDefaultRetryPolicy())
.AddPolicyHandler(ServiceCollectionExtensions.GetDefaultCircuitBreakerPolicy());

// Event Bus
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// Event Handlers
builder.Services.AddScoped<BusDeletedEventHandler>();

// CORS
builder.Services.AddDefaultCors(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<MenuDbContext>();

    // Response caching
    builder.Services.AddResponseCaching();

    var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Menu Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseGlobalExceptionHandler();

// app.UseHttpsRedirection(); // Not needed - API Gateway will handle HTTPS in production

    app.UseCors("DefaultCorsPolicy");

    app.UseResponseCaching();

    app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

// Subscribe to events
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    eventBus.Subscribe<BusDeletedEvent, BusDeletedEventHandler>();
}

// Database migration on startup (development only)
if (app.Environment.IsDevelopment())
{
    app.MigrateDatabaseInDevelopment<MenuDbContext>();
}

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.Run();

Log.Information("Menu Service shut down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Menu Service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
