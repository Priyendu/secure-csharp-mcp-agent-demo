using System.Text.Json;
using SecureMcpShared.Models;

namespace SecureMcpDesktopDemo.Services;

/// <summary>
/// Orchestrates a full release review using the MCP server.
/// This is the C# desktop equivalent of the web agent's ReleaseReviewOrchestrator.
/// 
/// It deliberately demonstrates the security boundary:
/// - The local parser only does lightweight intent extraction.
/// - All real data + authorization decisions live in the MCP server.
/// - approve_release will return 403 unless the token carries the privileged scope + secret was used at issuance time.
/// </summary>
public sealed class DesktopReviewOrchestrator
{
    private readonly McpDesktopClient _client;
    private readonly ReleaseIntentParser _parser;

    public DesktopReviewOrchestrator(McpDesktopClient client, ReleaseIntentParser parser)
    {
        _client = client;
        _parser = parser;
    }

    public async Task<ReviewResult> RunReviewAsync(
        string query,
        bool privileged,
        Action<ToolCallResult> onToolTrace,
        Action<string> onProtocol,
        CancellationToken ct = default)
    {
        // 1. Parse locally (demo of "agent" side)
        var intent = _parser.Parse(query);
        onProtocol?.Invoke($"[Orchestrator] Parsed intent: {intent.Component} v{intent.Version} (confidence {intent.Confidence:P0})");

        // 2. Acquire the right token
        var tokenResult = await _client.GetTokenAsync(privileged, ct);
        if (tokenResult.AccessToken is null)
        {
            onProtocol?.Invoke($"[Orchestrator] Token request failed: HTTP {tokenResult.HttpStatus}");
            return new ReviewResult("error", "Failed to obtain token from MCP server.", "Check server is running and /auth/token accepts the request.", intent, privileged ? "privileged" : "standard", "N/A", new List<ToolCallResult>());
        }

        string token = tokenResult.AccessToken;
        string mode = privileged ? "privileged" : "standard";

        var traces = new List<ToolCallResult>();

        // 3. Always run the three information tools (these only require mcp:tools)
        foreach (var tool in new[] { "get_release_status", "get_dependencies", "check_security_vulnerabilities" })
        {
            var trace = await _client.CallToolAsync(token, tool, intent.Component, intent.Version, ct);
            traces.Add(trace);
            onToolTrace?.Invoke(trace);
            await Task.Delay(120, ct); // small visual pacing for the UI
        }

        // 4. Decide whether to attempt approval (mirrors original web logic + always shows the auth boundary)
        var status = GetString(traces[0].Result, "status") ?? "unknown";
        var vulnCount = GetArrayLength(traces[2].Result, "vulnerabilities");

        bool shouldAttemptApprove = status.Equals("ready", StringComparison.OrdinalIgnoreCase) && vulnCount == 0;

        if (shouldAttemptApprove || true) // We *always* attempt approve in the demo so the 403 is visible when using standard token
        {
            var approveTrace = await _client.CallToolAsync(token, "approve_release", intent.Component, intent.Version, ct);
            traces.Add(approveTrace);
            onToolTrace?.Invoke(approveTrace);
        }

        // 5. Determine verdict exactly like the original orchestrator
        string verdict = DetermineVerdict(status, vulnCount, traces.LastOrDefault(t => t.Tool == "approve_release"));

        string summary = BuildSummary(verdict, intent, status, vulnCount, traces);
        string recommendation = BuildRecommendation(verdict);

        return new ReviewResult(
            Verdict: verdict,
            Summary: summary,
            Recommendation: recommendation,
            Intent: intent,
            ApprovalMode: mode,
            LlmMode: "deterministic (local C# parser)",
            ToolCalls: traces
        );
    }

    private static string DetermineVerdict(string status, int vulnerabilityCount, ToolCallResult? approvalTrace)
    {
        if (!status.Equals("ready", StringComparison.OrdinalIgnoreCase) || vulnerabilityCount > 0)
            return "blocked";

        if (approvalTrace is not null && approvalTrace.HttpStatus == 403)
            return "needs_approval_scope";

        if (approvalTrace is { Status: "ok" })
            return "approved";

        return "ready";
    }

    private static string BuildSummary(string verdict, ReleaseIntent intent, string status, int vulnerabilityCount, IReadOnlyList<ToolCallResult> traces)
    {
        return verdict switch
        {
            "blocked" => $"{intent.Component} {intent.Version} is blocked. Release status is {status} and {vulnerabilityCount} vulnerability finding(s) were returned by the secure MCP server.",
            "needs_approval_scope" => $"{intent.Component} {intent.Version} appears release-ready (status={status}, vulns={vulnerabilityCount}), but approval was denied with 403 because the token only carried the 'mcp:tools' scope.",
            "approved" => $"{intent.Component} {intent.Version} passed all checks. The MCP server accepted the privileged approval and returned success.",
            _ => $"{intent.Component} {intent.Version} review completed. Status={status}, vulns={vulnerabilityCount}."
        };
    }

    private static string BuildRecommendation(string verdict) => verdict switch
    {
        "blocked" => "Do not release. Resolve the blocking status or security vulnerabilities first. The MCP server is the source of truth.",
        "needs_approval_scope" => "This is the expected negative authorization path. Use the 'Privileged demo token' (which sends the demo-release-secret) to successfully call approve_release.",
        "approved" => "Release is approved in this demo. The privileged scope + secret combination was required and accepted by the server.",
        _ => "Review the full tool trace and raw responses above."
    };

    private static string? GetString(JsonElement? element, string property)
    {
        if (element is not { } value || !value.TryGetProperty(property, out var p))
            return null;
        return p.GetString();
    }

    private static int GetArrayLength(JsonElement? element, string property)
    {
        if (element is not { } value || !value.TryGetProperty(property, out var p))
            return 0;
        return p.ValueKind == JsonValueKind.Array ? p.GetArrayLength() : 0;
    }
}

// ReviewResult, ReleaseIntent, and ToolCallResult are now provided by SecureMcpShared.Models
// (no local duplicates).
