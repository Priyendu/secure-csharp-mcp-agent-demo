namespace SecureMcpClient.Tests;

public class PlannerTests
{
    [Fact]
    public void ParseQueryRecognizesAnalyzerServiceRelease()
    {
        var plan = Program.ParseQuery("Can we release AnalyzerService version 2.4.1?");

        Assert.Equal("AnalyzerService", plan.Component);
        Assert.Equal("2.4.1", plan.Version);
    }

    [Fact]
    public void ParseQueryFallsBackForUnknownComponent()
    {
        var plan = Program.ParseQuery("Can we release SomethingElse?");

        Assert.Equal("Unknown", plan.Component);
        Assert.Equal("1.0.0", plan.Version);
    }
}
