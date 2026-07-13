using Muxi;
using Xunit;

namespace Muxi.Tests;

public class SseParserTests
{
    [Fact]
    public void FlushesEventOnlyDoneFrames()
    {
        var parser = new SseEventParser();

        Assert.Null(parser.ProcessLine(": keepalive"));
        Assert.Null(parser.ProcessLine(""));
        Assert.Null(parser.ProcessLine("event: done"));

        var evt = parser.ProcessLine("");

        Assert.NotNull(evt);
        Assert.Equal("done", evt!.Event);
        Assert.Equal(string.Empty, evt.Data);
    }

    [Fact]
    public void PreservesMultilineData()
    {
        var parser = new SseEventParser();

        parser.ProcessLine("event: planning");
        parser.ProcessLine("data: one");
        parser.ProcessLine("data: two");

        var evt = parser.ProcessLine("");

        Assert.NotNull(evt);
        Assert.Equal("planning", evt!.Event);
        Assert.Equal("one\ntwo", evt.Data);
    }

    [Fact]
    public void RouteLevelErrorsBecomeExceptions()
    {
        var evt = new SseEvent("error", "{\"error\":\"boom\",\"type\":\"RUNTIME_ERROR\"}");

        var ex = Assert.Throws<MuxiException>(() => SseEventParser.ThrowIfRouteError(evt));

        Assert.Equal("RUNTIME_ERROR", ex.ErrorCode);
        Assert.Equal(0, ex.StatusCode);
    }

    [Fact]
    public void ParseUiWidgetsDecodesUiFrame()
    {
        var evt = new SseEvent("ui",
            "{\"ui\":[{\"type\":\"options\",\"id\":\"w1\",\"prompt\":\"Which?\"," +
            "\"options\":[{\"value\":\"us\",\"label\":\"United States\"}]}," +
            "{\"type\":\"action_link\",\"id\":\"w2\",\"label\":\"Dash\",\"url\":\"https://x.io\"}]}");

        var widgets = FormationClient.ParseUiWidgets(evt);

        Assert.Equal(2, widgets.Count);
        Assert.Equal("options", widgets[0]!["type"]!.GetValue<string>());
        Assert.Equal("United States", widgets[0]!["options"]![0]!["label"]!.GetValue<string>());
        Assert.Equal("https://x.io", widgets[1]!["url"]!.GetValue<string>());
    }

    [Fact]
    public void ParseUiWidgetsIgnoresOtherFrames()
    {
        Assert.Empty(FormationClient.ParseUiWidgets(new SseEvent("message", "hi")));
        Assert.Empty(FormationClient.ParseUiWidgets(new SseEvent("ui", "not json")));
        Assert.Empty(FormationClient.ParseUiWidgets(new SseEvent("ui", "{\"ui\":{}}")));
    }

    [Fact]
    public void UnwrapEnvelopeSurfacesIdempotencyKey()
    {
        var env = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"object\":\"api_response\",\"timestamp\":123," +
            "\"request\":{\"id\":\"req-1\",\"idempotency_key\":\"idem-42\"}," +
            "\"data\":{\"foo\":\"bar\"},\"success\":true}");

        var outNode = Transport.UnwrapEnvelope(env)!.AsObject();

        Assert.Equal("bar", outNode["foo"]!.GetValue<string>());
        Assert.Equal("req-1", outNode["request_id"]!.GetValue<string>());
        Assert.Equal("idem-42", outNode["idempotency_key"]!.GetValue<string>());
    }

    [Fact]
    public void UnwrapEnvelopeOmitsIdempotencyKeyWhenAbsent()
    {
        var env = System.Text.Json.Nodes.JsonNode.Parse(
            "{\"object\":\"api_response\",\"request\":{\"id\":\"req-1\"}," +
            "\"data\":{\"foo\":\"bar\"},\"success\":true}");

        var outNode = Transport.UnwrapEnvelope(env)!.AsObject();

        Assert.False(outNode.ContainsKey("idempotency_key"));
    }
}
