using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SecureMcpClient;

class Program
{
    static async Task Main(string[] args)
    {
        var serverUrl = "https://localhost:5001";
        var query = args.Length > 0 ? args[0] : "Can we release AnalyzerService version 2.4.1?";

        Console.WriteLine($"Secure MCP Client - Query: {query}");

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(serverUrl);

        // Get token (in real impl would be OAuth)
        var token = await GetToken(httpClient);
        Console.WriteLine($"Acquired token: {token.Substring(0, 20)}...");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Parse query (simple rule-based planner)
        var (component, version) = ParseQuery(query);
        Console.WriteLine($"Planning: {component} v{version}");

        // Execute sequence
        await CallTool(httpClient, "get_release_status", component, version);
        await CallTool(httpClient, "get_dependencies", component, version);
        await CallTool(httpClient, "check_security_vulnerabilities", component, version);

        // Final step requires release scope - will fail without it (demo)
        try
        {
            await CallTool(httpClient, "approve_release", component, version);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Expected auth failure on approve: {ex.Message}");
        }
    }

    static async Task<string> GetToken(HttpClient client)
    {
        var req = new { ClientId = "demo-client", Scopes = new[] { "mcp:tools" } };
        var resp = await client.PostAsJsonAsync("/auth/token", req);
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        return doc.GetProperty("access_token").GetString()!;
    }

    static (string, string) ParseQuery(string query)
    {
        // Very simple rule-based extraction
        if (query.Contains("AnalyzerService"))
            return ("AnalyzerService", "2.4.1");
        return ("Unknown", "1.0.0");
    }

    static async Task CallTool(HttpClient client, string tool, string component, string version)
    {
        Console.WriteLine($"\n>>> Calling tool: {tool}");

        var payload = new
        {
            jsonrpc = "2.0",
            id = new Random().Next(1000),
            method = "tools/call",
            @params = new
            {
                name = tool,
                arguments = new { component, version }
            }
        };

        try
        {
            var resp = await client.PostAsJsonAsync("/mcp/messages", payload);
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {body}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}