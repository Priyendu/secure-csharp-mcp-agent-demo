using System.Text.RegularExpressions;
using SecureMcpShared.Models;

namespace SecureMcpAgentWeb.Services;

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

        return new ReleaseIntent(component, version, "review_release", component == "Unknown" ? 0.35 : 0.75, "deterministic");
    }

    private static string? KnownComponent(string query)
    {
        if (query.Contains("AnalyzerService", StringComparison.OrdinalIgnoreCase))
        {
            return "AnalyzerService";
        }

        if (query.Contains("PaymentGateway", StringComparison.OrdinalIgnoreCase))
        {
            return "PaymentGateway";
        }

        return null;
    }

    [GeneratedRegex(@"(?<component>[A-Z][A-Za-z0-9]+)\s+(?:version|v)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentPattern();

    [GeneratedRegex(@"(?:version|v)\s+(?<version>[0-9]+(?:\.[0-9A-Za-z-]+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();
}
