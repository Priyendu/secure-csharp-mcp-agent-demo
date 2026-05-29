using System.Security.Claims;
using System.Text.Json;

namespace SecureMcpServer.Tools;

public class ReleaseDataService
{
    private readonly JsonDocument _demoData;

    public ReleaseDataService()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "demo-data.json");
        if (!File.Exists(dataPath))
        {
            dataPath = Path.Combine(Directory.GetCurrentDirectory(), "demo-data.json");
        }

        var json = File.Exists(dataPath) ? File.ReadAllText(dataPath) : DefaultDemoData;
        _demoData = JsonDocument.Parse(json);
    }

    public object GetReleaseStatus(string component, string version)
    {
        var release = FindRelease(component, version);
        return new
        {
            component,
            version,
            status = release?.GetProperty("status").GetString() ?? "unknown",
            lastUpdated = DateTime.UtcNow.ToString("O"),
            approvers = release is { } releaseData && releaseData.TryGetProperty("approvers", out var approvers)
                ? approvers.EnumerateArray().Select(item => item.GetString()).OfType<string>().ToArray()
                : Array.Empty<string>()
        };
    }

    public object GetDependencies(string component, string version)
    {
        var release = FindRelease(component, version);
        return new
        {
            component,
            version,
            dependencies = ReadArray(release, "dependencies")
        };
    }

    public object CheckVulnerabilities(string component, string version)
    {
        var release = FindRelease(component, version);
        return new
        {
            component,
            version,
            vulnerabilities = ReadArray(release, "vulnerabilities"),
            scanDate = DateTime.UtcNow.ToString("O")
        };
    }

    public Task<object> ApproveReleaseAsync(string component, string version, ClaimsPrincipal user)
    {
        if (!user.HasClaim("scope", "mcp:tools:release"))
        {
            throw new UnauthorizedAccessException("Release approval requires mcp:tools:release scope");
        }

        var approval = new
        {
            component,
            version,
            approved = true,
            approvedBy = user.Identity?.Name,
            approvedAt = DateTime.UtcNow.ToString("O"),
            ticket = $"REL-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
        };

        return Task.FromResult<object>(approval);
    }

    private JsonElement? FindRelease(string component, string version)
    {
        if (!_demoData.RootElement.TryGetProperty("releases", out var releases))
        {
            return null;
        }

        foreach (var release in releases.EnumerateArray())
        {
            var componentMatches = release.TryGetProperty("component", out var componentValue)
                && string.Equals(componentValue.GetString(), component, StringComparison.OrdinalIgnoreCase);
            var versionMatches = release.TryGetProperty("version", out var versionValue)
                && string.Equals(versionValue.GetString(), version, StringComparison.OrdinalIgnoreCase);

            if (componentMatches && versionMatches)
            {
                return release;
            }
        }

        return null;
    }

    private static object[] ReadArray(JsonElement? release, string propertyName)
    {
        if (release is not { } releaseData || !releaseData.TryGetProperty(propertyName, out var items))
        {
            return Array.Empty<object>();
        }

        return items.EnumerateArray()
            .Select(item => JsonSerializer.Deserialize<object>(item.GetRawText())!)
            .ToArray();
    }

    private const string DefaultDemoData = """
    {
      "releases": [
        {
          "component": "AnalyzerService",
          "version": "2.4.1",
          "status": "ready",
          "approvers": [ "release-bot", "security-team" ],
          "dependencies": [
            { "name": "Newtonsoft.Json", "version": "13.0.3", "status": "ok" },
            { "name": "Microsoft.AspNetCore", "version": "8.0.4", "status": "ok" }
          ],
          "vulnerabilities": []
        }
      ]
    }
    """;
}
