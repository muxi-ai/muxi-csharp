using System.Text.Json.Nodes;

namespace Muxi;

public class ServerConfig
{
    public required string Url { get; init; }
    public required string KeyId { get; init; }
    public required string SecretKey { get; init; }
    public int MaxRetries { get; init; } = 0;
    public int Timeout { get; init; } = 30;
    public bool Debug { get; init; } = false;
}

public class ServerClient : IDisposable
{
    private readonly Transport _transport;

    public ServerClient(ServerConfig config)
    {
        _transport = new Transport(
            config.Url,
            config.KeyId,
            config.SecretKey,
            config.Timeout,
            config.MaxRetries,
            config.Debug
        );
    }

    // Unauthenticated
    public async Task<int> PingAsync(CancellationToken ct = default)
    {
        var resp = await _transport.RequestJsonAsync("GET", "/ping", cancellationToken: ct);
        return resp is JsonObject obj ? obj.Count : 0;
    }

    public async Task<JsonNode?> HealthAsync(CancellationToken ct = default)
        => await _transport.RequestJsonAsync("GET", "/health", cancellationToken: ct);

    // Authenticated - Server management
    public async Task<JsonNode?> StatusAsync(CancellationToken ct = default)
        => await RpcGetAsync("/rpc/server/status", ct);

    public async Task<JsonNode?> ListFormationsAsync(CancellationToken ct = default)
        => await RpcGetAsync("/rpc/formations", ct);

    public async Task<JsonNode?> GetFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcGetAsync($"/rpc/formations/{formationId}", ct);

    public async Task<JsonNode?> StopFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/stop", new { }, ct);

    public async Task<JsonNode?> StartFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/start", new { }, ct);

    public async Task<JsonNode?> RestartFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/restart", new { }, ct);

    public async Task<JsonNode?> RollbackFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/rollback", new { }, ct);

    public async Task<JsonNode?> DeleteFormationAsync(string formationId, CancellationToken ct = default)
        => await RpcDeleteAsync($"/rpc/formations/{formationId}", ct);

    public async Task<JsonNode?> CancelUpdateAsync(string formationId, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/cancel-update", new { }, ct);

    public async Task<JsonNode?> DeployFormationAsync(string formationId, object payload, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/deploy", payload, ct);

    public async Task<JsonNode?> UpdateFormationAsync(string formationId, object payload, CancellationToken ct = default)
        => await RpcPostAsync($"/rpc/formations/{formationId}/update", payload, ct);

    public async Task<JsonNode?> GetFormationLogsAsync(string formationId, int? limit = null, CancellationToken ct = default)
    {
        var parameters = limit.HasValue ? new Dictionary<string, object?> { ["limit"] = limit } : null;
        return await RpcGetAsync($"/rpc/formations/{formationId}/logs", ct, parameters);
    }

    public async Task<JsonNode?> GetServerLogsAsync(int? limit = null, CancellationToken ct = default)
    {
        var parameters = limit.HasValue ? new Dictionary<string, object?> { ["limit"] = limit } : null;
        return await RpcGetAsync("/rpc/server/logs", ct, parameters);
    }

    // Streaming
    public IAsyncEnumerable<SseEvent> DeployFormationStreamAsync(string formationId, object payload, CancellationToken ct = default)
        => StreamSseAsync($"/rpc/formations/{formationId}/deploy/stream", payload, ct);

    public IAsyncEnumerable<SseEvent> UpdateFormationStreamAsync(string formationId, object payload, CancellationToken ct = default)
        => StreamSseAsync($"/rpc/formations/{formationId}/update/stream", payload, ct);

    public IAsyncEnumerable<SseEvent> StartFormationStreamAsync(string formationId, CancellationToken ct = default)
        => StreamSseAsync($"/rpc/formations/{formationId}/start/stream", new { }, ct);

    public IAsyncEnumerable<SseEvent> RestartFormationStreamAsync(string formationId, CancellationToken ct = default)
        => StreamSseAsync($"/rpc/formations/{formationId}/restart/stream", new { }, ct);

    public IAsyncEnumerable<SseEvent> RollbackFormationStreamAsync(string formationId, CancellationToken ct = default)
        => StreamSseAsync($"/rpc/formations/{formationId}/rollback/stream", new { }, ct);

    public IAsyncEnumerable<SseEvent> StreamFormationLogsAsync(string formationId, CancellationToken ct = default)
        => StreamSseGetAsync($"/rpc/formations/{formationId}/logs/stream", ct);

    private async Task<JsonNode?> RpcGetAsync(string path, CancellationToken ct, Dictionary<string, object?>? parameters = null)
        => await _transport.RequestJsonAsync("GET", path, parameters, cancellationToken: ct);

    private async Task<JsonNode?> RpcPostAsync(string path, object body, CancellationToken ct)
        => await _transport.RequestJsonAsync("POST", path, body: body, cancellationToken: ct);

    private async Task<JsonNode?> RpcDeleteAsync(string path, CancellationToken ct)
        => await _transport.RequestJsonAsync("DELETE", path, cancellationToken: ct);

    private async IAsyncEnumerable<SseEvent> StreamSseAsync(string path, object body, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string? currentEvent = null;
        var dataParts = new List<string>();

        await foreach (var line in _transport.StreamLinesAsync("POST", path, body: body, cancellationToken: ct))
        {
            if (line.StartsWith(":"))
                continue;

            if (string.IsNullOrEmpty(line))
            {
                if (dataParts.Count > 0)
                    yield return new SseEvent(currentEvent ?? "message", string.Join("\n", dataParts));
                currentEvent = null;
                dataParts.Clear();
                continue;
            }

            if (line.StartsWith("event:"))
                currentEvent = line[6..].Trim();
            else if (line.StartsWith("data:"))
                dataParts.Add(line[5..].Trim());
        }
    }

    private async IAsyncEnumerable<SseEvent> StreamSseGetAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string? currentEvent = null;
        var dataParts = new List<string>();

        await foreach (var line in _transport.StreamLinesAsync("GET", path, cancellationToken: ct))
        {
            if (line.StartsWith(":"))
                continue;

            if (string.IsNullOrEmpty(line))
            {
                if (dataParts.Count > 0)
                    yield return new SseEvent(currentEvent ?? "message", string.Join("\n", dataParts));
                currentEvent = null;
                dataParts.Clear();
                continue;
            }

            if (line.StartsWith("event:"))
                currentEvent = line[6..].Trim();
            else if (line.StartsWith("data:"))
                dataParts.Add(line[5..].Trim());
        }
    }

    public void Dispose() => _transport.Dispose();
}

public record SseEvent(string Event, string Data);
