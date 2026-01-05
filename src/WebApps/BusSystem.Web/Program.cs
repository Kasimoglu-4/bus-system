using System.Globalization;
using BusSystem.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Configure Turkish culture for currency formatting
var turkishCulture = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;

// Add response compression for production
builder.Services.AddDefaultResponseCompression();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    
    // Enable response compression in production
    app.UseResponseCompression();
}

app.UseHttpsRedirection();

// Configure static file options with caching for production
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static assets for 30 days in production
        if (!app.Environment.IsDevelopment())
        {
            var headers = ctx.Context.Response.Headers;
            var path = ctx.Context.Request.Path.Value?.ToLower() ?? "";
            
            // Long cache for versioned assets and images
            if (path.Contains("/lib/") || path.Contains("/images/") || 
                path.Contains(".webp") || path.Contains(".png") || 
                path.Contains(".jpg") || path.Contains(".jpeg") ||
                path.Contains(".ico"))
            {
                headers["Cache-Control"] = "public,max-age=2592000"; // 30 days
                headers["Expires"] = DateTime.UtcNow.AddDays(30).ToString("R");
            }
            // Shorter cache for CSS/JS (in case of updates)
            else if (path.Contains(".css") || path.Contains(".js"))
            {
                headers["Cache-Control"] = "public,max-age=86400"; // 1 day
            }
        }
    }
};

app.UseStaticFiles(staticFileOptions);

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
