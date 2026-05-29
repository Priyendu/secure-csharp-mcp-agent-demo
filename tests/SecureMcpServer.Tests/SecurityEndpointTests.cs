using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SecureMcpServer.Tests;

public class SecurityEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SecurityEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
                builder.ConfigureLogging(logging => logging.ClearProviders());
            })
            .CreateClient();
    }

    [Fact]
    public async Task TokenEndpointRejectsReleaseScopeWithoutDemoSecret()
    {
        var response = await _client.PostAsJsonAsync("/auth/token", new
        {
            ClientId = "demo-client",
            Scopes = new[] { "mcp:tools", "mcp:tools:release" }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveReleaseRequiresReleaseScope()
    {
        var token = await GetToken("mcp:tools");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/mcp/messages", ToolCall("approve_release"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("mcp:tools:release", body);
    }

    [Fact]
    public async Task ApproveReleaseSucceedsWithPrivilegedDemoSecret()
    {
        var token = await GetToken("mcp:tools", "mcp:tools:release");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/mcp/messages", ToolCall("approve_release"));
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"approved\":true", body);
    }

    [Fact]
    public async Task ToolDiscoveryMatchesImplementedTools()
    {
        var token = await GetToken("mcp:tools");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var discovery = await _client.GetFromJsonAsync<JsonElement>("/mcp/tools");
        var rpcListResponse = await _client.PostAsJsonAsync("/mcp/messages", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list"
        });
        var rpcList = JsonSerializer.Deserialize<JsonElement>(await rpcListResponse.Content.ReadAsStringAsync());

        var discoveryNames = ReadToolNames(discovery.GetProperty("tools"));
        var rpcNames = ReadToolNames(rpcList.GetProperty("result").GetProperty("tools"));

        Assert.Equal(discoveryNames, rpcNames);
        Assert.Equal(
            new[] { "get_release_status", "get_dependencies", "check_security_vulnerabilities", "approve_release" },
            discoveryNames);
    }

    [Fact]
    public async Task ReleaseDataComesFromDemoJson()
    {
        var token = await GetToken("mcp:tools");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/mcp/messages", ToolCall(
            "check_security_vulnerabilities",
            component: "PaymentGateway",
            version: "5.8.0"));
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("CVE-DEMO-2026-0001", body);
    }

    private async Task<string> GetToken(params string[] scopes)
    {
        var request = new
        {
            ClientId = "demo-client",
            Scopes = scopes,
            ClientSecret = scopes.Contains("mcp:tools:release") ? "demo-release-secret" : null
        };

        var response = await _client.PostAsJsonAsync("/auth/token", request);
        response.EnsureSuccessStatusCode();
        var payload = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return payload.GetProperty("access_token").GetString()!;
    }

    private static object ToolCall(
        string toolName,
        string component = "AnalyzerService",
        string version = "2.4.1") => new
        {
            jsonrpc = "2.0",
            id = 42,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = new { component, version }
            }
        };

    private static string[] ReadToolNames(JsonElement tools) =>
        tools.EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToArray();
}
