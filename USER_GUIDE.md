# MUXI C# SDK User Guide

## Installation

```bash
dotnet add package Muxi
```

## Requirements

- .NET 6.0+

## Quickstart

```csharp
using Muxi;

// Server client (management, HMAC auth)
var server = new ServerClient(new ServerConfig
{
    Url = "https://server.example.com",
    KeyId = "<key_id>",
    SecretKey = "<secret_key>"
});
Console.WriteLine(await server.StatusAsync());

// Formation client (runtime, key auth)
var formation = new FormationClient(new FormationConfig
{
    ServerUrl = "https://server.example.com",
    FormationId = "<formation_id>",
    ClientKey = "<client_key>",
    AdminKey = "<admin_key>"
});
Console.WriteLine(await formation.HealthAsync());
```

## Clients

- **ServerClient** (management, HMAC): deploy/list/update formations, server health/status, server logs.
- **FormationClient** (runtime, client/admin keys): chat/audio (streaming), agents, secrets, MCP, memory, scheduler, sessions/requests, identifiers, credentials, triggers/SOPs/audit, async/A2A/logging config, overlord/LLM settings, events/logs streaming.

## Streaming

```csharp
// Chat streaming with IAsyncEnumerable
await foreach (var chunk in formation.ChatStreamAsync(new { message = "Tell me a story" }, "user-123"))
{
    Console.Write(chunk.Data);
}

// Event streaming
await foreach (var ev in formation.StreamEventsAsync("user-123"))
{
    Console.WriteLine(ev);
}

// Log streaming (admin)
await foreach (var log in formation.StreamLogsAsync(new Dictionary<string, object?> { ["level"] = "info" }))
{
    Console.WriteLine(log);
}
```

## Auth & Headers

- **ServerClient**: HMAC with `KeyId`/`SecretKey` on `/rpc` endpoints.
- **FormationClient**: `X-MUXI-CLIENT-KEY` or `X-MUXI-ADMIN-KEY` on `/api/{formation}/v1`. Override `BaseUrl` for direct access (e.g., `http://localhost:9012/v1`).
- **Idempotency**: `X-Muxi-Idempotency-Key` auto-generated on every request.
- **SDK headers**: `X-Muxi-SDK`, `X-Muxi-Client` set automatically.

## Timeouts & Retries

- Default timeout: 30s (no timeout for streaming).
- Retries: `MaxRetries` with exponential backoff on 429/5xx/connection errors; respects `Retry-After`.
- Debug logging: enabled when `Debug = true` or `MUXI_DEBUG=1`.

## Error Handling

```csharp
try
{
    await formation.ChatAsync(new { message = "hello" });
}
catch (AuthenticationException e)
{
    Console.WriteLine($"Auth failed: {e.Message}");
}
catch (RateLimitException e)
{
    Console.WriteLine($"Rate limited. Retry after: {e.RetryAfter}s");
}
catch (NotFoundException e)
{
    Console.WriteLine($"Not found: {e.Message}");
}
catch (MuxiException e)
{
    Console.WriteLine($"{e.Code}: {e.Message} ({e.StatusCode})");
}
```

Error types: `AuthenticationException`, `AuthorizationException`, `NotFoundException`, `ValidationException`, `RateLimitException`, `ServerException`, `ConnectionException`.

## Notable Endpoints (FormationClient)

| Category | Methods |
|----------|---------|
| Chat/Audio | `ChatAsync`, `ChatStreamAsync`, `AudioChatAsync`, `AudioChatStreamAsync` |
| Memory | `GetMemoryConfigAsync`, `GetMemoriesAsync`, `AddMemoryAsync`, `DeleteMemoryAsync`, `GetUserBufferAsync`, `ClearUserBufferAsync`, `ClearSessionBufferAsync`, `ClearAllBuffersAsync`, `GetBufferStatsAsync` |
| Scheduler | `GetSchedulerConfigAsync`, `GetSchedulerJobsAsync`, `GetSchedulerJobAsync`, `CreateSchedulerJobAsync`, `DeleteSchedulerJobAsync` |
| Sessions | `GetSessionsAsync`, `GetSessionAsync`, `GetSessionMessagesAsync`, `RestoreSessionAsync` |
| Requests | `GetRequestsAsync`, `GetRequestStatusAsync`, `CancelRequestAsync` |
| Agents/MCP | `GetAgentsAsync`, `GetAgentAsync`, `GetMcpServersAsync`, `GetMcpServerAsync`, `GetMcpToolsAsync` |
| Secrets | `GetSecretsAsync`, `GetSecretAsync`, `SetSecretAsync`, `DeleteSecretAsync` |
| Credentials | `ListCredentialServicesAsync`, `ListCredentialsAsync`, `GetCredentialAsync`, `CreateCredentialAsync`, `DeleteCredentialAsync` |
| Identifiers | `GetUserIdentifiersForUserAsync`, `LinkUserIdentifierAsync`, `UnlinkUserIdentifierAsync` |
| Triggers/SOP | `GetTriggersAsync`, `GetTriggerAsync`, `FireTriggerAsync`, `GetSopsAsync`, `GetSopAsync` |
| Audit | `GetAuditLogAsync`, `ClearAuditLogAsync` |
| Config | `GetStatusAsync`, `GetConfigAsync`, `GetFormationInfoAsync`, `GetAsyncConfigAsync`, `GetA2aConfigAsync`, `GetLoggingConfigAsync`, `GetLoggingDestinationsAsync`, `GetOverlordConfigAsync`, `GetOverlordSoulAsync`, `GetLlmSettingsAsync` |
| Streaming | `StreamEventsAsync`, `StreamLogsAsync`, `StreamRequestAsync` |
| User | `ResolveUserAsync` |

## Webhook Verification

```csharp
using Muxi;

[HttpPost("/webhooks/muxi")]
public IActionResult HandleWebhook()
{
    using var reader = new StreamReader(Request.Body);
    var payload = reader.ReadToEnd();
    var signature = Request.Headers["X-Muxi-Signature"].FirstOrDefault();

    if (!Webhook.VerifySignature(payload, signature, Environment.GetEnvironmentVariable("WEBHOOK_SECRET")!))
    {
        return Unauthorized("Invalid signature");
    }

    var ev = Webhook.Parse(payload);

    switch (ev.Status)
    {
        case "completed":
            foreach (var item in ev.Content.Where(c => c.Type == "text"))
                Console.WriteLine(item.Text);
            break;
        case "failed":
            Console.WriteLine($"Error: {ev.Error?.Message}");
            break;
        case "awaiting_clarification":
            Console.WriteLine($"Question: {ev.Clarification?.Question}");
            break;
    }

    return Ok(new { status = "received" });
}
```

## Testing Locally

```bash
cd csharp
dotnet test
```
