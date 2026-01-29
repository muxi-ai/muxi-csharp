using Xunit;

namespace Muxi.Tests;

public class ErrorsTests
{
    [Fact]
    public void Map_401_ReturnsAuthenticationException()
    {
        var error = ErrorMapper.Map(401, "INVALID_KEY", "Invalid API key");
        
        Assert.IsType<AuthenticationException>(error);
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("INVALID_KEY", error.ErrorCode);
    }

    [Fact]
    public void Map_403_ReturnsAuthorizationException()
    {
        var error = ErrorMapper.Map(403, "FORBIDDEN", "Access denied");
        
        Assert.IsType<AuthorizationException>(error);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void Map_404_ReturnsNotFoundException()
    {
        var error = ErrorMapper.Map(404, "NOT_FOUND", "Resource not found");
        
        Assert.IsType<NotFoundException>(error);
        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public void Map_409_ReturnsConflictException()
    {
        var error = ErrorMapper.Map(409, "CONFLICT", "Already exists");
        
        Assert.IsType<ConflictException>(error);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public void Map_422_ReturnsValidationException()
    {
        var error = ErrorMapper.Map(422, "VALIDATION_ERROR", "Invalid input");
        
        Assert.IsType<ValidationException>(error);
        Assert.Equal(422, error.StatusCode);
    }

    [Fact]
    public void Map_429_ReturnsRateLimitExceptionWithRetryAfter()
    {
        var error = ErrorMapper.Map(429, null, "Rate limited", null, 60);
        
        var rateLimitError = Assert.IsType<RateLimitException>(error);
        Assert.Equal(429, rateLimitError.StatusCode);
        Assert.Equal(60, rateLimitError.RetryAfter);
    }

    [Fact]
    public void Map_5xx_ReturnsServerException()
    {
        var error = ErrorMapper.Map(500, "INTERNAL", "Server error");
        
        Assert.IsType<ServerException>(error);
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public void Map_UnknownStatus_ReturnsMuxiException()
    {
        var error = ErrorMapper.Map(418, "TEAPOT", "I'm a teapot");
        
        Assert.IsType<MuxiException>(error);
        Assert.IsNotType<AuthenticationException>(error);
    }
}
