# MUXI .NET SDK

Official .NET SDK for the MUXI AI platform.

## Requirements

- .NET 8.0+

## Installation

```bash
dotnet add package Muxi
```

## Quick Start

### Server Management (Control Plane)

```csharp
using Muxi;

var server = new ServerClient(new ServerConfig
{
    Url = Environment.GetEnvironmentVariable("MUXI_SERVER_URL")!,
    KeyId = Environment.GetEnvironmentVariable("MUXI_KEY_ID")!,
    SecretKey = Environment.GetEnvironmentVariable("MUXI_SECRET_KEY")!
});

// List formations
var formations = await server.ListFormationsAsync();
Console.WriteLine(formations);

// Get server status
var status = await server.StatusAsync();
Console.WriteLine($"Uptime: {status?["uptime"]}s");
```

### Formation Usage (Runtime API)

```csharp
using Muxi;

// Connect via server proxy
var client = new FormationClient(new FormationConfig
{
    FormationId = "my-bot",
    ServerUrl = Environment.GetEnvironmentVariable("MUXI_SERVER_URL"),
    AdminKey = Environment.GetEnvironmentVariable("MUXI_ADMIN_KEY"),
    ClientKey = Environment.GetEnvironmentVariable("MUXI_CLIENT_KEY")
});

// Or connect directly to formation
var client = new FormationClient(new FormationConfig
{
    Url = "http://localhost:8001",
    AdminKey = Environment.GetEnvironmentVariable("MUXI_ADMIN_KEY"),
    ClientKey = Environment.GetEnvironmentVariable("MUXI_CLIENT_KEY")
});

// Chat (non-streaming)
var response = await client.ChatAsync(new { message = "Hello!" }, "user123");
Console.WriteLine(response?["message"]);

// Chat (streaming)
await foreach (var evt in client.ChatStreamAsync(new { message = "Tell me a story" }, "user123"))
{
    Console.Write(evt.Data);
}

// Health check
var health = await client.HealthAsync();
Console.WriteLine($"Status: {health?["status"]}");
```

## Webhook Verification

```csharp
using Muxi;

// In your webhook handler (e.g., ASP.NET Core controller)
[HttpPost("webhook")]
public IActionResult HandleWebhook()
{
    using var reader = new StreamReader(Request.Body);
    var payload = reader.ReadToEnd();
    var signature = Request.Headers["X-Muxi-Signature"].FirstOrDefault();
    var secret = Environment.GetEnvironmentVariable("WEBHOOK_SECRET")!;

    if (!Webhook.VerifySignature(payload, signature, secret))
    {
        return Unauthorized("Invalid signature");
    }

    var evt = Webhook.Parse(payload);

    switch (evt.Status)
    {
        case "completed":
            foreach (var item in evt.Content)
            {
                if (item.Type == "text")
                    Console.WriteLine(item.Text);
            }
            break;
        case "failed":
            Console.WriteLine($"Error: {evt.Error?.Message}");
            break;
        case "awaiting_clarification":
            Console.WriteLine($"Question: {evt.Clarification?.Question}");
            break;
    }

    return Ok(new { received = true });
}
```

## Configuration

### Environment Variables

- `MUXI_DEBUG=1` - Enable debug logging

### Client Options

```csharp
var server = new ServerClient(new ServerConfig
{
    Url = "https://muxi.example.com:7890",
    KeyId = "your-key-id",
    SecretKey = "your-secret-key",
    Timeout = 30,      // Request timeout in seconds
    MaxRetries = 3,    // Retry on 429/5xx errors
    Debug = true       // Enable debug logging
});
```

## Error Handling

```csharp
try
{
    await server.GetFormationAsync("nonexistent");
}
catch (NotFoundException e)
{
    Console.WriteLine($"Not found: {e.Message}");
}
catch (AuthenticationException e)
{
    Console.WriteLine($"Auth failed: {e.Message}");
}
catch (RateLimitException e)
{
    Console.WriteLine($"Rate limited. Retry after: {e.RetryAfter}s");
}
catch (MuxiException e)
{
    Console.WriteLine($"Error: {e.Message} ({e.StatusCode})");
}
```

## License

MIT
