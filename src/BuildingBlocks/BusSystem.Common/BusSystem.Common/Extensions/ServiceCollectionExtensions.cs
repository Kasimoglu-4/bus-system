using System.Text;
using BusSystem.Common.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Events;

namespace BusSystem.Common.Extensions;

/// <summary>
/// Extension methods for configuring common services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add common middleware for all microservices
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }

    /// <summary>
    /// Add CORS policy for microservices
    /// </summary>
    public static IServiceCollection AddDefaultCors(this IServiceCollection services, IConfiguration? configuration = null, string policyName = "DefaultCorsPolicy")
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, builder =>
            {
                // Check if specific origins are configured
                var allowedOrigins = configuration?.GetSection("Cors:AllowedOrigins").Get<string[]>();
                
                if (allowedOrigins != null && allowedOrigins.Length > 0)
                {
                    // Use configured origins
                    builder.WithOrigins(allowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
                else
                {
                    // Default to allow any origin for development
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Add JWT Authentication with standard configuration
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration,
        bool includeRoleClaimType = false)
    {
        var jwtSecret = configuration["Jwt:Secret"] 
            ?? throw new InvalidOperationException("JWT Secret not configured");
        var jwtIssuer = configuration["Jwt:Issuer"] 
            ?? throw new InvalidOperationException("JWT Issuer not configured");
        var jwtAudience = configuration["Jwt:Audience"] 
            ?? throw new InvalidOperationException("JWT Audience not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            if (includeRoleClaimType)
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };

            // Only set RoleClaimType if explicitly requested (can't be null)
            if (includeRoleClaimType)
            {
                options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
            }
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Add Swagger/OpenAPI with JWT Authentication support
    /// </summary>
    public static IServiceCollection AddSwaggerWithJwtAuth(
        this IServiceCollection services,
        string title,
        string version = "v1",
        string? description = null)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(version, new OpenApiInfo
            {
                Title = title,
                Version = version,
                Description = description
            });

            // Add JWT Authentication to Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Add SQL Server DbContext with retry policy
    /// </summary>
    public static IServiceCollection AddSqlServerWithRetry<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection") where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(connectionStringName),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null
                    );
                    // Optimize connection pooling for production
                    sqlOptions.CommandTimeout(30);
                }
            ).EnableSensitiveDataLogging(configuration.GetValue<bool>("EnableSensitiveDataLogging", false))
        );

        return services;
    }

    /// <summary>
    /// Migrate database in development environment
    /// </summary>
    public static IApplicationBuilder MigrateDatabaseInDevelopment<TContext>(
        this IApplicationBuilder app) where TContext : DbContext
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();
        
        try
        {
            dbContext.Database.Migrate();
            logger.LogInformation("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database");
        }

        return app;
    }

    /// <summary>
    /// Add response compression with standard configuration
    /// </summary>
    public static IServiceCollection AddDefaultResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = new[]
            {
                "text/plain",
                "text/css",
                "application/javascript",
                "text/html",
                "application/json",
                "text/json",
                "image/svg+xml",
                "application/xml"
            };
        });

        return services;
    }

    /// <summary>
    /// Configure Serilog for structured logging
    /// </summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, string serviceName)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: $"logs/{serviceName}-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30)
            .CreateLogger();

        builder.Host.UseSerilog();
        
        return builder;
    }

    /// <summary>
    /// Get default HTTP retry policy with exponential backoff
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning("Retry {RetryCount} after {Delay}s due to {Exception}", 
                        retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Get default circuit breaker policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetDefaultCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5, 
                TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    Log.Error("Circuit breaker opened for {Duration}s due to {Exception}", 
                        duration.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    Log.Information("Circuit breaker reset");
                });
    }
}

