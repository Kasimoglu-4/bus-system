using Microsoft.AspNetCore.StaticFiles;
using BusSystem.FileStorage.API.Services;
using BusSystem.Common.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogLogging("FileStorageService");

try
{
    Log.Information("Starting FileStorage Service");

    // Add services to the container
    builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register image optimization service
builder.Services.AddScoped<IImageOptimizationService, ImageOptimizationService>();

// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration, includeRoleClaimType: true);

// Add response compression for production
builder.Services.AddDefaultResponseCompression();

// Add CORS
builder.Services.AddDefaultCors(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable compression in production
    app.UseResponseCompression();
}

app.UseCors("DefaultCorsPolicy");

// Serve static files from the uploads directory with caching
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/files",
    ContentTypeProvider = new FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        }
    },
    OnPrepareResponse = ctx =>
    {
        // Cache images for 30 days in production
        if (!app.Environment.IsDevelopment())
        {
            var headers = ctx.Context.Response.Headers;
            headers["Cache-Control"] = "public,max-age=2592000,immutable";
            headers["Expires"] = DateTime.UtcNow.AddDays(30).ToString("R");
        }
    }
});

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

Log.Information("FileStorage Service shut down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "FileStorage Service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
