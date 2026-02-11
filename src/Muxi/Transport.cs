using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Muxi;

public class Transport : IDisposable
{
    private static readonly int[] RetryStatuses = { 429, 500, 502, 503, 504 };
    
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly string _keyId;
    private readonly string _secretKey;
    private readonly int _timeout;
    private readonly int _maxRetries;
    private readonly bool _debug;
    private readonly string? _app;

    public Transport(string baseUrl, string keyId, string secretKey, int timeout = 30, int maxRetries = 0, bool debug = false, string? app = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _keyId = keyId?.Trim() ?? "";
        _secretKey = secretKey?.Trim() ?? "";
        _timeout = timeout;
        _maxRetries = maxRetries;
        _debug = debug || Environment.GetEnvironmentVariable("MUXI_DEBUG") == "1";
        _app = app;
        
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
    }

    public async Task<JsonNode?> RequestJsonAsync(string method, string path, Dictionary<string, object?>? queryParams = null, object? body = null, CancellationToken cancellationToken = default)
    {
        var (url, fullPath) = BuildUrl(path, queryParams);
        var headers = BuildHeaders(method, fullPath);

        var attempt = 0;
        var backoff = 0.5;

        while (true)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), url);
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);

                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _client.SendAsync(request, cancellationToken);
                var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                Log($"{method} {fullPath} -> {(int)response.StatusCode} ({elapsed:F3}s)");

                // Check for SDK updates (non-blocking, once per process)
                VersionCheck.CheckForUpdates(response);

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var retryAfter = response.Headers.RetryAfter?.Delta?.Seconds;

                    if (RetryStatuses.Contains(statusCode) && attempt < _maxRetries)
                    {
                        var sleepFor = Math.Min(backoff, 30);
                        Log($"retry {method} {fullPath} after {sleepFor}s due to {statusCode}");
                        await Task.Delay(TimeSpan.FromSeconds(sleepFor), cancellationToken);
                        backoff *= 2;
                        attempt++;
                        continue;
                    }

                    ThrowError(statusCode, responseBody, (int?)retryAfter);
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrEmpty(content))
                    return null;

                try
                {
                    var parsed = JsonNode.Parse(content);
                    return UnwrapEnvelope(parsed);
                }
                catch
                {
                    return JsonValue.Create(content);
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt < _maxRetries)
                {
                    var sleepFor = Math.Min(backoff, 30);
                    Log($"retry {method} {fullPath} after {sleepFor}s due to connection error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(sleepFor), cancellationToken);
                    backoff *= 2;
                    attempt++;
                    continue;
                }
                throw new ConnectionException(ex.Message);
            }
        }
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(string method, string path, Dictionary<string, object?>? queryParams = null, object? body = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (url, fullPath) = BuildUrl(path, queryParams);
        var headers = BuildHeaders(method, fullPath, "text/event-stream");

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            yield return line;
        }
    }

    private (string Url, string FullPath) BuildUrl(string path, Dictionary<string, object?>? queryParams)
    {
        var relPath = path.StartsWith("/") ? path : $"/{path}";
        var query = "";
        if (queryParams != null)
        {
            var filtered = queryParams.Where(kvp => kvp.Value != null)
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}");
            var queryStr = string.Join("&", filtered);
            if (!string.IsNullOrEmpty(queryStr))
                query = $"?{queryStr}";
        }
        var fullPath = relPath + query;
        return ($"{_baseUrl}{fullPath}", fullPath);
    }

    private Dictionary<string, string> BuildHeaders(string method, string path, string accept = "application/json")
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = Auth.BuildAuthHeader(_keyId, _secretKey, method, path),
            ["Content-Type"] = "application/json",
            ["Accept"] = accept,
            ["X-Muxi-SDK"] = $"csharp/{MuxiVersion.Version}",
            ["X-Muxi-Client"] = $"dotnet/{Environment.Version}",
            ["X-Muxi-Idempotency-Key"] = Guid.NewGuid().ToString()
        };
        if (!string.IsNullOrEmpty(_app)) headers["X-Muxi-App"] = _app;
        return headers;
    }

    private static JsonNode? UnwrapEnvelope(JsonNode? obj)
    {
        if (obj is not JsonObject jsonObj || !jsonObj.ContainsKey("data"))
            return obj;

        var req = jsonObj["request"]?.AsObject();
        var requestId = req?["id"]?.GetValue<string>() ?? jsonObj["request_id"]?.GetValue<string>();
        var ts = jsonObj["timestamp"];
        var data = jsonObj["data"];

        if (data is JsonObject dataObj)
        {
            if (requestId != null && !dataObj.ContainsKey("request_id"))
                dataObj["request_id"] = requestId;
            if (ts != null && !dataObj.ContainsKey("timestamp"))
                dataObj["timestamp"] = ts?.DeepClone();
            return dataObj;
        }

        return data ?? obj;
    }

    private static void ThrowError(int statusCode, string responseBody, int? retryAfter)
    {
        string? code = null;
        string message = "Unknown error";
        Dictionary<string, object>? details = null;

        try
        {
            var payload = JsonNode.Parse(responseBody);
            code = payload?["code"]?.GetValue<string>() ?? payload?["error"]?.GetValue<string>();
            message = payload?["message"]?.GetValue<string>() ?? message;
            if (payload is JsonObject obj)
                details = obj.ToDictionary(kvp => kvp.Key, kvp => (object)(kvp.Value?.ToString() ?? ""));
        }
        catch { }

        throw ErrorMapper.Map(statusCode, code, message, details, retryAfter);
    }

    private void Log(string message)
    {
        if (_debug)
            Console.Error.WriteLine($"[MUXI] {message}");
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
