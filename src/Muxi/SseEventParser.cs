using System.Text.Json.Nodes;

namespace Muxi;

internal sealed class SseEventParser
{
    private string? _currentEvent;
    private readonly List<string> _dataParts = [];

    public SseEvent? ProcessLine(string line)
    {
        if (line.StartsWith(":")) return null;
        if (string.IsNullOrEmpty(line)) return Flush();

        var (field, value) = SplitField(line);
        if (field == "event") _currentEvent = value;
        else if (field == "data") _dataParts.Add(value);

        return null;
    }

    public SseEvent? Flush()
    {
        if (_currentEvent is null && _dataParts.Count == 0) return null;

        var evt = new SseEvent(_currentEvent ?? "message", string.Join("\n", _dataParts));
        _currentEvent = null;
        _dataParts.Clear();
        return evt;
    }

    public static void ThrowIfRouteError(SseEvent evt)
    {
        if (!string.Equals(evt.Event, "error", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string code = "STREAM_ERROR";
        var message = string.IsNullOrEmpty(evt.Data) ? "stream error" : evt.Data;
        Dictionary<string, object>? details = null;

        try
        {
            if (JsonNode.Parse(evt.Data) is JsonObject jsonObject)
            {
                code = jsonObject["type"]?.GetValue<string>()
                    ?? jsonObject["code"]?.GetValue<string>()
                    ?? jsonObject["error"]?.GetValue<string>()
                    ?? code;
                message = jsonObject["error"]?.GetValue<string>()
                    ?? jsonObject["message"]?.GetValue<string>()
                    ?? message;
            }
        }
        catch
        {
        }

        throw new MuxiException(code, message, 0, details);
    }

    private static (string Field, string Value) SplitField(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0) return (line, string.Empty);

        var field = line[..idx];
        var value = line[(idx + 1)..];
        if (value.Length > 0 && value[0] == ' ') value = value[1..];
        return (field, value);
    }
}
