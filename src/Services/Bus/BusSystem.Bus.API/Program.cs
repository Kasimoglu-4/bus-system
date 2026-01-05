using BusSystem.Bus.API.Data;
using BusSystem.Bus.API.Services;
using BusSystem.Common.Extensions;
using BusSystem.EventBus.Abstractions;
using BusSystem.EventBus.InMemory;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogLogging("BusService");

try
{
    Log.Information("Starting Bus Service");

    // Add services to the container
    builder.Services.AddControllers();

// Swagger/OpenAPI configuration
builder.Services.AddSwaggerWithJwtAuth(
    title: "BusSystem Bus Service API",
    description: "Bus management service with QR code generation for Bus System microservices");

// Database configuration
builder.Services.AddSqlServerWithRetry<BusDbContext>(builder.Configuration);

// JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Application services
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// CORS
builder.Services.AddDefaultCors(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<BusDbContext>();

    // Response caching
    builder.Services.AddResponseCaching();

    var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bus Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Add Serilog request logging 
app.UseSerilogRequestLogging();

app.UseGlobalExceptionHandler();

// app.UseHttpsRedirection();

// Serve static files (for QR codes)
app.UseStaticFiles();

    app.UseCors("DefaultCorsPolicy");

    app.UseResponseCaching();

    app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

// Database migration on startup (development only)
if (app.Environment.IsDevelopment())
{
    app.MigrateDatabaseInDevelopment<BusDbContext>();
}

app.Run();

Log.Information("Bus Service shut down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bus Service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
