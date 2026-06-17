using System.Text.RegularExpressions;

namespace SecureMcpDesktopDemo.Services;

/// <summary>
/// Deterministic intent parser (ported/adapted from the web agent).
/// This demonstrates that natural language understanding can be local and simple
/// for the demo while the *authorization* is still enforced strictly by the MCP server.
/// </summary>
public sealed partial class ReleaseIntentParser
{
    public ReleaseIntent Parse(string query)
    {
        var component = KnownComponent(query);
        if (component is null)
        {
            var componentMatch = ComponentPattern().Match(query);
            component = componentMatch.Success ? componentMatch.Groups["component"].Value : "Unknown";
        }

        var versionMatch = VersionPattern().Match(query);
        var version = versionMatch.Success ? versionMatch.Groups["version"].Value : "1.0.0";

        // Confidence is higher for known demo components
        double confidence = component == "Unknown" ? 0.35 : 0.82;
        return new ReleaseIntent(component, version, "review_release", confidence, "deterministic-local");
    }

    private static string? KnownComponent(string query)
    {
        if (query.Contains("AnalyzerService", StringComparison.OrdinalIgnoreCase))
            return "AnalyzerService";

        if (query.Contains("PaymentGateway", StringComparison.OrdinalIgnoreCase))
            return "PaymentGateway";

        return null;
    }

    [GeneratedRegex(@"(?<component>[A-Z][A-Za-z0-9]+)\s+(?:version|v)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentPattern();

    [GeneratedRegex(@"(?:version|v)\s+(?<version>[0-9]+(?:\.[0-9A-Za-z-]+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();
}

public sealed record ReleaseIntent(
    string Component,
    string Version,
    string Intent,
    double Confidence,
    string Source);
