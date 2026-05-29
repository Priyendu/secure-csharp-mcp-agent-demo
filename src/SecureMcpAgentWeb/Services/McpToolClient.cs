using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SecureMcpAgentWeb.Services;

public sealed class McpToolClient
{
    private readonly HttpClient _httpClient;
    private readonly AgentOptions _options;

    public McpToolClient(HttpClient httpClient, IOptions<AgentOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.McpServerUrl);
    }

    public async Task<string> GetTokenAsync(bool privileged, CancellationToken cancellationToken)
    {
        var scopes = privileged
            ? new[] { "mcp:tools", "mcp:tools:release" }
            : new[] { "mcp:tools" };
        var response = await _httpClient.PostAsJsonAsync("/auth/token", new
        {
            ClientId = "agent-web",
            Scopes = scopes,
            ClientSecret = privileged ? _options.DemoReleaseClientSecret : null
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        return token?.AccessToken ?? throw new InvalidOperationException("MCP token response did not include access_token.");
    }

    public async Task<ToolTrace> CallToolAsync(string token, string tool, string component, string version, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = Random.Shared.Next(1000, 9999),
            method = "tools/call",
            @params = new
            {
                name = tool,
                arguments = new { component, version }
            }
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(raw);
        var root = document.RootElement.Clone();
        var status = response.IsSuccessStatusCode ? "ok" : "error";
        var result = root.TryGetProperty("result", out var resultElement) ? resultElement.Clone() : (JsonElement?)null;
        var error = root.TryGetProperty("error", out var errorElement) ? errorElement.Clone() : (JsonElement?)null;

        return new ToolTrace(tool, status, (int)response.StatusCode, result, error);
    }
}
