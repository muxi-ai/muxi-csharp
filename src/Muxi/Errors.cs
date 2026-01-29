namespace Muxi;

public class MuxiException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    public Dictionary<string, object>? Details { get; }

    public MuxiException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(string.IsNullOrEmpty(errorCode) ? message : $"{errorCode}: {message}")
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details;
    }
}

public class AuthenticationException : MuxiException
{
    public AuthenticationException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class AuthorizationException : MuxiException
{
    public AuthorizationException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class NotFoundException : MuxiException
{
    public NotFoundException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class ConflictException : MuxiException
{
    public ConflictException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class ValidationException : MuxiException
{
    public ValidationException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class RateLimitException : MuxiException
{
    public int? RetryAfter { get; }

    public RateLimitException(string message, int statusCode, int? retryAfter = null, Dictionary<string, object>? details = null)
        : base("RATE_LIMITED", message, statusCode, details)
    {
        RetryAfter = retryAfter;
    }
}

public class ServerException : MuxiException
{
    public ServerException(string errorCode, string message, int statusCode, Dictionary<string, object>? details = null)
        : base(errorCode, message, statusCode, details) { }
}

public class ConnectionException : MuxiException
{
    public ConnectionException(string message)
        : base("CONNECTION_ERROR", message, 0) { }
}

public static class ErrorMapper
{
    public static MuxiException Map(int status, string? code, string message, Dictionary<string, object>? details = null, int? retryAfter = null)
    {
        return status switch
        {
            401 => new AuthenticationException(code ?? "UNAUTHORIZED", message, status, details),
            403 => new AuthorizationException(code ?? "FORBIDDEN", message, status, details),
            404 => new NotFoundException(code ?? "NOT_FOUND", message, status, details),
            409 => new ConflictException(code ?? "CONFLICT", message, status, details),
            422 => new ValidationException(code ?? "VALIDATION_ERROR", message, status, details),
            429 => new RateLimitException(message ?? "Too Many Requests", status, retryAfter, details),
            >= 500 and < 600 => new ServerException(code ?? "SERVER_ERROR", message, status, details),
            _ => new MuxiException(code ?? "ERROR", message, status, details),
        };
    }
}
