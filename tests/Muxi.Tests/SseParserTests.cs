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
}
