using BusSystem.Common.Extensions;
using BusSystem.Identity.API.Data;
using BusSystem.Identity.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogLogging("IdentityService");

try
{
    Log.Information("Starting Identity Service");

    // Add services to the container
    builder.Services.AddControllers();

// Swagger/OpenAPI configuration
builder.Services.AddSwaggerWithJwtAuth(
    title: "BusSystem Identity Service API",
    description: "Identity and Authentication service for Bus System microservices architecture");

// Database configuration
builder.Services.AddSqlServerWithRetry<IdentityDbContext>(builder.Configuration);

// JWT Authentication (with role claim type for Identity service)
builder.Services.AddJwtAuthentication(builder.Configuration, includeRoleClaimType: true);

// Application services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// CORS
builder.Services.AddDefaultCors(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<IdentityDbContext>();

    // Response caching
    builder.Services.AddResponseCaching();

    var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API v1");
        c.RoutePrefix = string.Empty; // Swagger at root
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

// Database migration on startup (for development only)
if (app.Environment.IsDevelopment())
{
    app.MigrateDatabaseInDevelopment<IdentityDbContext>();
}

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.Run();

Log.Information("Identity Service shut down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity Service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
