namespace BusSystem.Common.Exceptions;

/// <summary>
/// Base exception for all custom exceptions
/// </summary>
public abstract class BaseException : Exception
{
    public int StatusCode { get; set; }
    public List<string>? Errors { get; set; }

    protected BaseException(string message, int statusCode = 500) : base(message)
    {
        StatusCode = statusCode;
    }

    protected BaseException(string message, List<string> errors, int statusCode = 500) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}

/// <summary>
/// Exception for resource not found (404)
/// </summary>
public class NotFoundException : BaseException
{
    public NotFoundException(string message) : base(message, 404)
    {
    }

    public NotFoundException(string resourceName, object key) 
        : base($"{resourceName} with key '{key}' was not found.", 404)
    {
    }
}

/// <summary>
/// Exception for validation errors (400)
/// </summary>
public class ValidationException : BaseException
{
    public ValidationException(string message) : base(message, 400)
    {
    }

    public ValidationException(List<string> errors) 
        : base("One or more validation errors occurred.", errors, 400)
    {
    }
}

/// <summary>
/// Exception for unauthorized access (401)
/// </summary>
public class UnauthorizedException : BaseException
{
    public UnauthorizedException(string message = "Unauthorized access.") : base(message, 401)
    {
    }
}

/// <summary>
/// Exception for forbidden access (403)
/// </summary>
public class ForbiddenException : BaseException
{
    public ForbiddenException(string message = "Access forbidden.") : base(message, 403)
    {
    }
}

/// <summary>
/// Exception for business logic violations
/// </summary>
public class BusinessException : BaseException
{
    public BusinessException(string message) : base(message, 422)
    {
    }
}

