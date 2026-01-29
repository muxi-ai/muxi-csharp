using Xunit;

namespace Muxi.Tests;

public class AuthTests
{
    [Fact]
    public void GenerateHmacSignature_ReturnsValidSignatureAndTimestamp()
    {
        var (signature, timestamp) = Auth.GenerateHmacSignature("secret", "GET", "/test");
        
        Assert.NotEmpty(signature);
        Assert.InRange(timestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
    }

    [Fact]
    public void BuildAuthHeader_ReturnsProperlyFormattedHeader()
    {
        var header = Auth.BuildAuthHeader("key123", "secret", "POST", "/rpc/test");
        
        Assert.StartsWith("MUXI-HMAC key=key123, timestamp=", header);
        Assert.Contains("signature=", header);
    }

    [Fact]
    public void GenerateHmacSignature_StripsQueryParams()
    {
        var (sig1, _) = Auth.GenerateHmacSignature("secret", "GET", "/test");
        var (sig2, _) = Auth.GenerateHmacSignature("secret", "GET", "/test?foo=bar");
        
        Assert.Equal(sig1.Length, sig2.Length);
    }
}
