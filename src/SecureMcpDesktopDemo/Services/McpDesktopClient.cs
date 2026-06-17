using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecureMcpDesktopDemo.Services;

/// <summary>
/// Standalone MCP client for the desktop demo.
/// Handles:
/// - Demo token acquisition (standard vs privileged with secret)
/// - JSON-RPC tool calls over the authenticated /mcp/messages endpoint
/// - Dev certificate bypass (so the demo works out of the box with dotnet dev-certs)
/// - Protocol logging callback so the UI can show every wire-level interaction.
/// 
/// This client is deliberately "another client" (like the console client and the web agent)
/// to demonstrate that the security model lives on the server.
/// </summary>
public sealed class McpDesktopClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly Action<string>? _protocolLog;

    private const string DemoReleaseSecret = "demo-release-secret";

    public McpDesktopClient(string serverUrl = "https://localhost:5001", Action<string>? protocolLog = null)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _protocolLog = protocolLog;

        var handler = new HttpClientHandler
        {
            // Accept the development certificate used by the MCP server.
            // In a real system you would use proper PKI / trusted certs.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_serverUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Requests a token from the demo /auth/token endpoint.
    /// </summary>
    public async Task<TokenResult> GetTokenAsync(bool privileged, CancellationToken ct = default)
    {
        var clientId = "desktop-demo-client";
        var scopes = privileged
            ? new[] { "mcp:tools", "mcp:tools:release" }
            : new[] { "mcp:tools" };

        var payload = new
        {
            clientId,
            scopes,
            clientSecret = privileged ? DemoReleaseSecret : null
        };

        LogProtocol($"POST /auth/token  privileged={privileged}");
        LogProtocol($"Request body: {JsonSerializer.Serialize(payload)}");

        var response = await _httpClient.PostAsJsonAsync("/auth/token", payload, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        LogProtocol($"Response: HTTP {(int)response.StatusCode}  {body}");

        if (!response.IsSuccessStatusCode)
        {
            return new TokenResult(null, (int)response.StatusCode, body);
        }

        var tokenResp = JsonSerializer.Deserialize<TokenResponse>(body);
        return new TokenResult(tokenResp?.AccessToken, (int)response.StatusCode, body);
    }

    /// <summary>
    /// Calls one of the four MCP tools using the supplied bearer token.
    /// Returns rich trace information for UI display.
    /// </summary>
    public async Task<ToolCallResult> CallToolAsync(string token, string toolName, string component, string version, CancellationToken ct = default)
    {
        var requestId = Random.Shared.Next(10000, 99999);

        var jsonRpc = new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = new { component, version }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(jsonRpc);

        LogProtocol($"POST /mcp/messages  tool={toolName}  component={component}@{version}");
        LogProtocol($"JSON-RPC Request: {JsonSerializer.Serialize(jsonRpc)}");

        var response = await _httpClient.SendAsync(request, ct);
        var rawBody = await response.Content.ReadAsStringAsync(ct);

        LogProtocol($"Response: HTTP {(int)response.StatusCode}  {rawBody}");

        JsonElement? result = null;
        JsonElement? error = null;
        string status = response.IsSuccessStatusCode ? "ok" : "error";

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("result", out var r))
                result = r.Clone();
            if (root.TryGetProperty("error", out var e))
                error = e.Clone();
        }
        catch
        {
            // Non-JSON error body - keep raw in error
            error = JsonSerializer.SerializeToElement(new { message = rawBody });
        }

        return new ToolCallResult(
            Tool: toolName,
            HttpStatus: (int)response.StatusCode,
            Status: status,
            Result: result,
            Error: error,
            Raw: rawBody
        );
    }

    /// <summary>
    /// Optional: calls the /mcp/tools discovery endpoint (demonstrates the other discovery path).
    /// </summary>
    public async Task<string> GetToolsDiscoveryAsync(string token, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp/tools");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        LogProtocol($"GET /mcp/tools -> HTTP {(int)response.StatusCode}\n{body}");
        return body;
    }

    private void LogProtocol(string message)
    {
        _protocolLog?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // --- DTOs ---

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType);

    public sealed record TokenResult(string? AccessToken, int HttpStatus, string RawResponse);

    public sealed record ToolCallResult(
        string Tool,
        int HttpStatus,
        string Status,
        JsonElement? Result,
        JsonElement? Error,
        string Raw);
}
