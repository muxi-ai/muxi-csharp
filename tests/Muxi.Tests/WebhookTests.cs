using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Muxi.Tests;

public class WebhookTests
{
    private const string Secret = "test_webhook_secret";
    private const string Payload = """{"id":"req123","status":"completed","response":[{"type":"text","text":"Hello"}]}""";

    private static string CreateSignature(string payload, string secret, long? timestamp = null)
    {
        timestamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var message = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        var sigHeader = CreateSignature(Payload, Secret);
        
        Assert.True(Webhook.VerifySignature(Payload, sigHeader, Secret));
    }

    [Fact]
    public void VerifySignature_InvalidSignature_ReturnsFalse()
    {
        var sigHeader = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1=invalidsignature";
        
        Assert.False(Webhook.VerifySignature(Payload, sigHeader, Secret));
    }

    [Fact]
    public void VerifySignature_NullHeader_ReturnsFalse()
    {
        Assert.False(Webhook.VerifySignature(Payload, null, Secret));
    }

    [Fact]
    public void VerifySignature_EmptyHeader_ReturnsFalse()
    {
        Assert.False(Webhook.VerifySignature(Payload, "", Secret));
    }

    [Fact]
    public void VerifySignature_ExpiredTimestamp_ReturnsFalse()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600;
        var sigHeader = CreateSignature(Payload, Secret, oldTimestamp);
        
        Assert.False(Webhook.VerifySignature(Payload, sigHeader, Secret));
    }

    [Fact]
    public void VerifySignature_MissingSecret_ThrowsException()
    {
        Assert.Throws<WebhookVerificationException>(() => 
            Webhook.VerifySignature(Payload, "t=123,v1=abc", ""));
    }

    [Fact]
    public void Parse_CompletedPayload_ReturnsEvent()
    {
        var @event = Webhook.Parse(Payload);
        
        Assert.Equal("req123", @event.RequestId);
        Assert.Equal("completed", @event.Status);
        Assert.Single(@event.Content);
        Assert.Equal("text", @event.Content[0].Type);
        Assert.Equal("Hello", @event.Content[0].Text);
    }

    [Fact]
    public void Parse_FailedPayload_ReturnsEventWithError()
    {
        var payload = """{"id":"req456","status":"failed","error":{"code":"TIMEOUT","message":"Request timed out"}}""";
        
        var @event = Webhook.Parse(payload);
        
        Assert.Equal("failed", @event.Status);
        Assert.NotNull(@event.Error);
        Assert.Equal("TIMEOUT", @event.Error.Code);
        Assert.Equal("Request timed out", @event.Error.Message);
    }

    [Fact]
    public void Parse_ClarificationPayload_ReturnsEventWithClarification()
    {
        var payload = """{"id":"req789","status":"awaiting_clarification","clarification_question":"Which file do you mean?"}""";
        
        var @event = Webhook.Parse(payload);
        
        Assert.Equal("awaiting_clarification", @event.Status);
        Assert.NotNull(@event.Clarification);
        Assert.Equal("Which file do you mean?", @event.Clarification.Question);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsException()
    {
        Assert.Throws<WebhookVerificationException>(() => Webhook.Parse("not json"));
    }
}
