using SecureMcpAgentWeb.Services;

namespace SecureMcpAgentWeb.Tests;

public class ReleaseIntentParserTests
{
    private readonly ReleaseIntentParser _parser = new();

    [Fact]
    public void ParseRecognizesReadyDemoScenario()
    {
        var intent = _parser.Parse("Can we release AnalyzerService version 2.4.1?");

        Assert.Equal("AnalyzerService", intent.Component);
        Assert.Equal("2.4.1", intent.Version);
        Assert.Equal("review_release", intent.Intent);
        Assert.Equal("deterministic", intent.Source);
    }

    [Fact]
    public void ParseRecognizesBlockedDemoScenario()
    {
        var intent = _parser.Parse("Can we release PaymentGateway version 5.8.0?");

        Assert.Equal("PaymentGateway", intent.Component);
        Assert.Equal("5.8.0", intent.Version);
    }

    [Fact]
    public void ParseUsesFallbacksWhenIntentIsIncomplete()
    {
        var intent = _parser.Parse("Can we release this?");

        Assert.Equal("Unknown", intent.Component);
        Assert.Equal("1.0.0", intent.Version);
        Assert.True(intent.Confidence < 0.5);
    }
}
