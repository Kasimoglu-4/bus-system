using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using BusSystem.Admin.Web.Middleware;
using BusSystem.Common.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.AddSerilogLogging("AdminWeb");

try
{
    Log.Information("Starting Admin Web");

    // Add services to the container.
    builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpClient with resilience policies
builder.Services.AddHttpClient("default")
    .AddPolicyHandler(ServiceCollectionExtensions.GetDefaultRetryPolicy())
    .AddPolicyHandler(ServiceCollectionExtensions.GetDefaultCircuitBreakerPolicy());

builder.Services.AddHttpContextAccessor(); // Required for accessing HttpContext in controllers

// Configure Turkish culture for currency formatting
var turkishCulture = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;

// Add Cookie Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection();

// Serve static files from wwwroot
app.MapStaticAssets();

// Serve uploaded files from wwwroot/uploads
var uploadsPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseRouting();
app.UseSession();

app.UseAuthentication();

// Validate that authenticated user still exists in database
app.UseMiddleware<ValidateUserExistsMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AdminPanel}/{action=Index}/{id?}")
    .WithStaticAssets();

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.Run();

Log.Information("Admin Web shut down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Admin Web failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
