using System.Net;
using System.Text.Json;
using BusSystem.Common.Exceptions;
using BusSystem.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace BusSystem.Common.Middleware;

/// <summary>
/// Global exception handling middleware for all microservices
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate correlation ID for request tracking
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();
        
        context.Response.Headers.Append("X-Correlation-ID", correlationId);
        
        // Add correlation ID to log context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";
        var response = new ApiResponse<object>();

        switch (exception)
        {
            case NotFoundException notFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Success = false;
                response.Message = notFoundException.Message;
                _logger.LogWarning(notFoundException, 
                    "Resource not found: {Message} | CorrelationId: {CorrelationId}", 
                    notFoundException.Message, correlationId);
                break;

            case ValidationException validationException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Success = false;
                response.Message = validationException.Message;
                response.Errors = validationException.Errors;
                _logger.LogWarning(validationException, 
                    "Validation error: {Message} | CorrelationId: {CorrelationId}", 
                    validationException.Message, correlationId);
                break;

            case UnauthorizedException unauthorizedException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Success = false;
                response.Message = unauthorizedException.Message;
                _logger.LogWarning(unauthorizedException, 
                    "Unauthorized access: {Message} | CorrelationId: {CorrelationId}", 
                    unauthorizedException.Message, correlationId);
                break;

            case ForbiddenException forbiddenException:
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Success = false;
                response.Message = forbiddenException.Message;
                _logger.LogWarning(forbiddenException, 
                    "Forbidden access: {Message} | CorrelationId: {CorrelationId}", 
                    forbiddenException.Message, correlationId);
                break;

            case BusinessException businessException:
                context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                response.Success = false;
                response.Message = businessException.Message;
                _logger.LogWarning(businessException, 
                    "Business logic error: {Message} | CorrelationId: {CorrelationId}", 
                    businessException.Message, correlationId);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Success = false;
                response.Message = "An internal server error occurred.";
                _logger.LogError(exception, 
                    "Unhandled exception: {ExceptionType} {Message} | Path: {Path} | Method: {Method} | CorrelationId: {CorrelationId}", 
                    exception.GetType().Name, exception.Message, context.Request.Path, context.Request.Method, correlationId);
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

