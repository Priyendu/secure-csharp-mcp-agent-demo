using System.Security.Claims;
using System.Text.Json;

namespace SecureMcpServer.Tools;

public class ReleaseDataService
{
    private readonly Dictionary<string, object> _demoData;

    public ReleaseDataService()
    {
        _demoData = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText("demo-data.json")) 
            ?? new Dictionary<string, object>();
    }

    public object GetReleaseStatus(string component, string version)
    {
        return new
        {
            component,
            version,
            status = "ready",
            lastUpdated = DateTime.UtcNow.ToString("O"),
            approvers = new[] { "release-bot", "security-team" }
        };
    }

    public object GetDependencies(string component, string version)
    {
        return new
        {
            component,
            version,
            dependencies = new[]
            {
                new { name = "Newtonsoft.Json", version = "13.0.3", status = "ok" },
                new { name = "Microsoft.AspNetCore", version = "8.0.4", status = "ok" }
            }
        };
    }

    public object CheckVulnerabilities(string component, string version)
    {
        return new
        {
            component,
            version,
            vulnerabilities = Array.Empty<object>(),
            scanDate = DateTime.UtcNow.ToString("O")
        };
    }

    public async Task<object> ApproveReleaseAsync(string component, string version, ClaimsPrincipal user)
    {
        if (!user.HasClaim("scope", "mcp:tools:release"))
        {
            throw new UnauthorizedAccessException("Release approval requires mcp:tools:release scope");
        }

        return new
        {
            component,
            version,
            approved = true,
            approvedBy = user.Identity?.Name,
            approvedAt = DateTime.UtcNow.ToString("O"),
            ticket = $"REL-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
        };
    }
}