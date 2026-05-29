using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureMcpAgentWeb.Tests;

public class StaticUiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StaticUiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePageServesReleaseReviewConsole()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("Release Review Console", html);
        Assert.Contains("/app.js", html);
    }
}
