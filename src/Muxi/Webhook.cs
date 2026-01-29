using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Muxi;

public class WebhookVerificationException : Exception
{
    public WebhookVerificationException(string message) : base(message) { }
}

public record ContentItem(string Type, string? Text = null, JsonNode? File = null)
{
    public static ContentItem FromJson(JsonNode node) => new(
        node["type"]?.GetValue<string>() ?? "text",
        node["text"]?.GetValue<string>(),
        node["file"]
    );
}

public record ErrorDetails(string Code, string Message, string? Trace = null)
{
    public static ErrorDetails FromJson(JsonNode node) => new(
        node["code"]?.GetValue<string>() ?? "unknown",
        node["message"]?.GetValue<string>() ?? "Unknown error",
        node["trace"]?.GetValue<string>()
    );
}

public record Clarification(string Question, string? ClarificationRequestId = null, string? OriginalMessage = null)
{
    public static Clarification FromJson(JsonNode node) => new(
        node["clarification_question"]?.GetValue<string>() ?? "",
        node["clarification_request_id"]?.GetValue<string>(),
        node["original_message"]?.GetValue<string>()
    );
}

public record WebhookEvent(
    string RequestId,
    string Status,
    long Timestamp,
    IReadOnlyList<ContentItem> Content,
    ErrorDetails? Error,
    Clarification? Clarification,
    string? FormationId,
    string? UserId,
    double? ProcessingTime,
    string ProcessingMode,
    string? WebhookUrl,
    JsonNode Raw
)
{
    public static WebhookEvent FromJson(JsonNode node)
    {
        var content = (node["response"]?.AsArray() ?? new JsonArray())
            .Select(item => ContentItem.FromJson(item!))
            .ToList();

        var error = node["error"] != null ? ErrorDetails.FromJson(node["error"]!) : null;
        var clarification = node["status"]?.GetValue<string>() == "awaiting_clarification"
            ? Clarification.FromJson(node)
            : null;

        return new WebhookEvent(
            node["id"]?.GetValue<string>() ?? "",
            node["status"]?.GetValue<string>() ?? "unknown",
            node["timestamp"]?.GetValue<long>() ?? 0,
            content,
            error,
            clarification,
            node["formation_id"]?.GetValue<string>(),
            node["user_id"]?.GetValue<string>(),
            node["processing_time"]?.GetValue<double>(),
            node["processing_mode"]?.GetValue<string>() ?? "async",
            node["webhook_url"]?.GetValue<string>(),
            node
        );
    }
}

public static class Webhook
{
    public static bool VerifySignature(string payload, string? signatureHeader, string secret, int toleranceSeconds = 300)
    {
        if (string.IsNullOrEmpty(signatureHeader))
            return false;

        if (string.IsNullOrEmpty(secret))
            throw new WebhookVerificationException("Webhook secret is required");

        // Parse signature header: "t=1234567890,v1=abc123..."
        try
        {
            var parts = signatureHeader.Split(',')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]);

            if (!parts.TryGetValue("t", out var timestampStr) || !parts.TryGetValue("v1", out var signature))
                return false;

            if (!long.TryParse(timestampStr, out var timestamp))
                return false;

            // Check timestamp tolerance
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(currentTime - timestamp) > toleranceSeconds)
                return false;

            // Compute expected signature
            var message = $"{timestamp}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            // Constant-time comparison
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature)
            );
        }
        catch
        {
            return false;
        }
    }

    public static WebhookEvent Parse(string payload)
    {
        try
        {
            var node = JsonNode.Parse(payload);
            if (node == null)
                throw new WebhookVerificationException("Invalid JSON payload: null");
            return WebhookEvent.FromJson(node);
        }
        catch (JsonException ex)
        {
            throw new WebhookVerificationException($"Invalid JSON payload: {ex.Message}");
        }
    }

    public static WebhookEvent Parse(JsonNode payload)
    {
        return WebhookEvent.FromJson(payload);
    }
}
