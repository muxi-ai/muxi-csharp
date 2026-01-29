using System.Text.Json.Nodes;

namespace Muxi;

public class FormationConfig
{
    public string? FormationId { get; init; }
    public string? Url { get; init; }
    public string? ServerUrl { get; init; }
    public string? BaseUrl { get; init; }
    public string? AdminKey { get; init; }
    public string? ClientKey { get; init; }
    public int MaxRetries { get; init; } = 0;
    public int Timeout { get; init; } = 30;
    public bool Debug { get; init; } = false;
}

public class FormationClient : IDisposable
{
    private readonly FormationTransport _transport;

    public FormationClient(FormationConfig config)
    {
        var baseUrl = BuildBaseUrl(config);
        _transport = new FormationTransport(
            baseUrl,
            config.AdminKey,
            config.ClientKey,
            config.Timeout,
            config.MaxRetries,
            config.Debug
        );
    }

    // Health / status
    public Task<JsonNode?> HealthAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/health", useAdmin: false, ct: ct);
    public Task<JsonNode?> GetStatusAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/status", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/config", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetFormationInfoAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/formation", useAdmin: true, ct: ct);

    // Agents / MCP
    public Task<JsonNode?> GetAgentsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/agents", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetAgentAsync(string agentId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/agents/{agentId}", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetMcpServersAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/mcp/servers", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetMcpServerAsync(string serverId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/mcp/servers/{serverId}", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetMcpToolsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/mcp/tools", useAdmin: true, ct: ct);

    // Secrets
    public Task<JsonNode?> GetSecretsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/secrets", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetSecretAsync(string key, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/secrets/{key}", useAdmin: true, ct: ct);
    public Task SetSecretAsync(string key, string value, CancellationToken ct = default) => _transport.RequestAsync("PUT", $"/secrets/{key}", body: new { value }, useAdmin: true, ct: ct);
    public Task DeleteSecretAsync(string key, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/secrets/{key}", useAdmin: true, ct: ct);

    // Chat
    public Task<JsonNode?> ChatAsync(object payload, string userId = "", CancellationToken ct = default) => _transport.RequestAsync("POST", "/chat", body: payload, useAdmin: false, userId: userId, ct: ct);
    public IAsyncEnumerable<SseEvent> ChatStreamAsync(object payload, string userId = "", CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>((payload as IDictionary<string, object>) ?? new Dictionary<string, object>()) { ["stream"] = true };
        return _transport.StreamSseAsync("POST", "/chat", body: body, useAdmin: false, userId: userId, ct: ct);
    }
    public Task<JsonNode?> AudioChatAsync(object payload, string userId = "", CancellationToken ct = default) => _transport.RequestAsync("POST", "/audiochat", body: payload, useAdmin: false, userId: userId, ct: ct);

    // Sessions / requests
    public Task<JsonNode?> GetSessionsAsync(string userId, int? limit = null, CancellationToken ct = default) => _transport.RequestAsync("GET", "/sessions", new() { ["user_id"] = userId, ["limit"] = limit }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetSessionAsync(string sessionId, string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/sessions/{sessionId}", useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetSessionMessagesAsync(string sessionId, string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/sessions/{sessionId}/messages", useAdmin: false, userId: userId, ct: ct);
    public Task RestoreSessionAsync(string sessionId, string userId, object[] messages, CancellationToken ct = default) => _transport.RequestAsync("POST", $"/sessions/{sessionId}/restore", body: new { messages }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetRequestsAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", "/requests", useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetRequestStatusAsync(string requestId, string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/requests/{requestId}", useAdmin: false, userId: userId, ct: ct);
    public Task CancelRequestAsync(string requestId, string userId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/requests/{requestId}", useAdmin: false, userId: userId, ct: ct);

    // Memory
    public Task<JsonNode?> GetMemoryConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/memory", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetMemoriesAsync(string userId, int? limit = null, CancellationToken ct = default) => _transport.RequestAsync("GET", "/memories", new() { ["user_id"] = userId, ["limit"] = limit }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> AddMemoryAsync(string userId, string type, string detail, CancellationToken ct = default) => _transport.RequestAsync("POST", "/memories", body: new { user_id = userId, type, detail }, useAdmin: false, userId: userId, ct: ct);
    public Task DeleteMemoryAsync(string userId, string memoryId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/memories/{memoryId}", new() { ["user_id"] = userId }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetUserBufferAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", "/memory/buffer", new() { ["user_id"] = userId }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> ClearUserBufferAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", "/memory/buffer", new() { ["user_id"] = userId }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> ClearSessionBufferAsync(string userId, string sessionId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/memory/buffer/{sessionId}", new() { ["user_id"] = userId }, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> ClearAllBuffersAsync(CancellationToken ct = default) => _transport.RequestAsync("DELETE", "/memory/buffer", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetBufferStatsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/memory/stats", useAdmin: true, ct: ct);

    // Scheduler
    public Task<JsonNode?> GetSchedulerConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/scheduler", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetSchedulerJobsAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", "/scheduler/jobs", new() { ["user_id"] = userId }, useAdmin: true, ct: ct);
    public Task<JsonNode?> GetSchedulerJobAsync(string jobId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/scheduler/jobs/{jobId}", useAdmin: true, ct: ct);
    public Task<JsonNode?> CreateSchedulerJobAsync(string type, string schedule, string message, string userId, CancellationToken ct = default) => _transport.RequestAsync("POST", "/scheduler/jobs", body: new { type, schedule, message, user_id = userId }, useAdmin: true, ct: ct);
    public Task DeleteSchedulerJobAsync(string jobId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/scheduler/jobs/{jobId}", useAdmin: true, ct: ct);

    // Async / logging / a2a
    public Task<JsonNode?> GetAsyncConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/async", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetA2aConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/a2a", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetLoggingConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/logging", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetLoggingDestinationsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/logging/destinations", useAdmin: true, ct: ct);

    // Credentials / identifiers
    public Task<JsonNode?> ListCredentialServicesAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/credentials/services", useAdmin: true, ct: ct);
    public Task<JsonNode?> ListCredentialsAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", "/credentials", useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetCredentialAsync(string credentialId, string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/credentials/{credentialId}", useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> CreateCredentialAsync(string userId, object payload, CancellationToken ct = default) => _transport.RequestAsync("POST", "/credentials", body: payload, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> DeleteCredentialAsync(string credentialId, string userId, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/credentials/{credentialId}", useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetUserIdentifiersForUserAsync(string userId, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/users/identifiers/{userId}", useAdmin: true, ct: ct);
    public Task<JsonNode?> LinkUserIdentifierAsync(string muxiUserId, object[] identifiers, CancellationToken ct = default) => _transport.RequestAsync("POST", "/users/identifiers", body: new { muxi_user_id = muxiUserId, identifiers }, useAdmin: true, ct: ct);
    public Task UnlinkUserIdentifierAsync(string identifier, CancellationToken ct = default) => _transport.RequestAsync("DELETE", $"/users/identifiers/{identifier}", useAdmin: true, ct: ct);

    // Overlord / LLM
    public Task<JsonNode?> GetOverlordConfigAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/overlord", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetOverlordPersonaAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/overlord/persona", useAdmin: true, ct: ct);
    public Task<JsonNode?> GetLlmSettingsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/llm/settings", useAdmin: true, ct: ct);

    // Triggers / SOP / Audit
    public Task<JsonNode?> GetTriggersAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/triggers", useAdmin: false, ct: ct);
    public Task<JsonNode?> GetTriggerAsync(string name, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/triggers/{name}", useAdmin: false, ct: ct);
    public Task<JsonNode?> FireTriggerAsync(string name, object data, bool async_ = false, string userId = "", CancellationToken ct = default) => _transport.RequestAsync("POST", $"/triggers/{name}", new() { ["async"] = async_.ToString().ToLower() }, data, useAdmin: false, userId: userId, ct: ct);
    public Task<JsonNode?> GetSopsAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/sops", useAdmin: false, ct: ct);
    public Task<JsonNode?> GetSopAsync(string name, CancellationToken ct = default) => _transport.RequestAsync("GET", $"/sops/{name}", useAdmin: false, ct: ct);
    public Task<JsonNode?> GetAuditLogAsync(CancellationToken ct = default) => _transport.RequestAsync("GET", "/audit", useAdmin: true, ct: ct);
    public Task ClearAuditLogAsync(CancellationToken ct = default) => _transport.RequestAsync("DELETE", "/audit?confirm=clear-audit-log", useAdmin: true, ct: ct);

    // Streaming
    public IAsyncEnumerable<SseEvent> StreamEventsAsync(string userId, CancellationToken ct = default) => _transport.StreamSseAsync("GET", "/events", new() { ["user_id"] = userId }, useAdmin: false, userId: userId, ct: ct);
    public IAsyncEnumerable<SseEvent> StreamRequestAsync(string userId, string sessionId, string requestId, CancellationToken ct = default) => _transport.StreamSseAsync("GET", $"/events/{sessionId}/{requestId}", useAdmin: false, userId: userId, ct: ct);
    public IAsyncEnumerable<SseEvent> StreamLogsAsync(Dictionary<string, object?>? filters = null, CancellationToken ct = default) => _transport.StreamSseAsync("GET", "/logs", filters, useAdmin: true, ct: ct);

    // Resolve user
    public Task<JsonNode?> ResolveUserAsync(string identifier, bool createUser = false, CancellationToken ct = default) => _transport.RequestAsync("POST", "/users/resolve", body: new { identifier, create_user = createUser }, useAdmin: false, ct: ct);

    private static string BuildBaseUrl(FormationConfig config)
    {
        if (!string.IsNullOrEmpty(config.BaseUrl)) return config.BaseUrl.TrimEnd('/');
        if (!string.IsNullOrEmpty(config.Url)) return config.Url.TrimEnd('/') + "/v1";
        if (!string.IsNullOrEmpty(config.ServerUrl) && !string.IsNullOrEmpty(config.FormationId))
            return $"{config.ServerUrl.TrimEnd('/')}/api/{config.FormationId}/v1";
        throw new ArgumentException("must set BaseUrl, Url, or ServerUrl+FormationId");
    }

    public void Dispose() => _transport.Dispose();
}

internal class FormationTransport : IDisposable
{
    private static readonly int[] RetryStatuses = { 429, 500, 502, 503, 504 };
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly string? _adminKey;
    private readonly string? _clientKey;
    private readonly int _timeout;
    private readonly int _maxRetries;
    private readonly bool _debug;

    public FormationTransport(string baseUrl, string? adminKey, string? clientKey, int timeout, int maxRetries, bool debug)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _adminKey = adminKey?.Trim();
        _clientKey = clientKey?.Trim();
        _timeout = timeout;
        _maxRetries = maxRetries;
        _debug = debug || Environment.GetEnvironmentVariable("MUXI_DEBUG") == "1";
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
    }

    public async Task<JsonNode?> RequestAsync(string method, string path, Dictionary<string, object?>? queryParams = null, object? body = null, bool useAdmin = true, string userId = "", CancellationToken ct = default)
    {
        var (url, _) = BuildUrl(path, queryParams);
        var headers = BuildHeaders(useAdmin, userId, body != null ? "application/json" : null);

        var attempt = 0;
        var backoff = 0.5;

        while (true)
        {
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), url);
                foreach (var (key, value) in headers) request.Headers.TryAddWithoutValidation(key, value);
                if (body != null) request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

                var response = await _client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    if (RetryStatuses.Contains(statusCode) && attempt < _maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(backoff, 30)), ct);
                        backoff *= 2;
                        attempt++;
                        continue;
                    }
                    var responseBody = await response.Content.ReadAsStringAsync(ct);
                    ThrowError(statusCode, responseBody, (int?)response.Headers.RetryAfter?.Delta?.Seconds);
                }

                var content = await response.Content.ReadAsStringAsync(ct);
                return string.IsNullOrEmpty(content) ? null : UnwrapEnvelope(JsonNode.Parse(content));
            }
            catch (HttpRequestException ex)
            {
                if (attempt < _maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(backoff, 30)), ct);
                    backoff *= 2;
                    attempt++;
                    continue;
                }
                throw new ConnectionException(ex.Message);
            }
        }
    }

    public async IAsyncEnumerable<SseEvent> StreamSseAsync(string method, string path, Dictionary<string, object?>? queryParams = null, object? body = null, bool useAdmin = true, string userId = "", [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var (url, _) = BuildUrl(path, queryParams);
        var headers = BuildHeaders(useAdmin, userId, body != null ? "application/json" : null, "text/event-stream");

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        foreach (var (key, value) in headers) request.Headers.TryAddWithoutValidation(key, value);
        if (body != null) request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? currentEvent = null;
        var dataParts = new List<string>();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) continue;
            if (line.StartsWith(":")) continue;
            if (string.IsNullOrEmpty(line))
            {
                if (dataParts.Count > 0) yield return new SseEvent(currentEvent ?? "message", string.Join("\n", dataParts));
                currentEvent = null;
                dataParts.Clear();
                continue;
            }
            if (line.StartsWith("event:")) currentEvent = line[6..].Trim();
            else if (line.StartsWith("data:")) dataParts.Add(line[5..].Trim());
        }
    }

    private (string Url, string FullPath) BuildUrl(string path, Dictionary<string, object?>? queryParams)
    {
        var relPath = path.StartsWith("/") ? path : $"/{path}";
        var query = queryParams != null ? string.Join("&", queryParams.Where(kvp => kvp.Value != null).Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}")) : "";
        var fullPath = string.IsNullOrEmpty(query) ? relPath : $"{relPath}?{query}";
        return ($"{_baseUrl}{fullPath}", fullPath);
    }

    private Dictionary<string, string> BuildHeaders(bool useAdmin, string userId, string? contentType = null, string accept = "application/json")
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Muxi-SDK"] = $"csharp/{MuxiVersion.Version}",
            ["X-Muxi-Client"] = $"csharp/{MuxiVersion.Version}",
            ["X-Muxi-Idempotency-Key"] = Guid.NewGuid().ToString(),
            ["Accept"] = accept
        };
        if (useAdmin)
        {
            if (string.IsNullOrEmpty(_adminKey)) throw new ArgumentException("admin key required");
            headers["X-MUXI-ADMIN-KEY"] = _adminKey;
        }
        else
        {
            if (string.IsNullOrEmpty(_clientKey)) throw new ArgumentException("client key required");
            headers["X-MUXI-CLIENT-KEY"] = _clientKey;
        }
        if (!string.IsNullOrEmpty(userId)) headers["X-Muxi-User-ID"] = userId;
        if (contentType != null) headers["Content-Type"] = contentType;
        return headers;
    }

    private static JsonNode? UnwrapEnvelope(JsonNode? obj)
    {
        if (obj is not JsonObject jsonObj || !jsonObj.ContainsKey("data")) return obj;
        var data = jsonObj["data"];
        if (data is JsonObject dataObj)
        {
            var reqId = jsonObj["request"]?["id"]?.GetValue<string>() ?? jsonObj["request_id"]?.GetValue<string>();
            var ts = jsonObj["timestamp"];
            if (reqId != null && !dataObj.ContainsKey("request_id")) dataObj["request_id"] = reqId;
            if (ts != null && !dataObj.ContainsKey("timestamp")) dataObj["timestamp"] = ts?.DeepClone();
            return dataObj;
        }
        return data ?? obj;
    }

    private static void ThrowError(int statusCode, string responseBody, int? retryAfter)
    {
        string? code = null; string message = "Unknown error"; Dictionary<string, object>? details = null;
        try { var p = JsonNode.Parse(responseBody); code = p?["code"]?.GetValue<string>() ?? p?["error"]?.GetValue<string>(); message = p?["message"]?.GetValue<string>() ?? message; } catch { }
        throw ErrorMapper.Map(statusCode, code, message, details, retryAfter);
    }

    public void Dispose() => _client.Dispose();
}
