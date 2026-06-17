using System.Text.Json;
using SecureMcpShared.Models;

namespace SecureMcpAgentWeb.Services;

public sealed class ReleaseReviewOrchestrator
{
    private readonly OpenAiReleasePlanner _planner;
    private readonly McpToolClient _mcpToolClient;

    public ReleaseReviewOrchestrator(OpenAiReleasePlanner planner, McpToolClient mcpToolClient)
    {
        _planner = planner;
        _mcpToolClient = mcpToolClient;
    }

    public async Task<ReviewResult> ReviewAsync(ReleaseReviewRequest request, CancellationToken cancellationToken)
    {
        var privileged = string.Equals(request.ApprovalMode, "privileged", StringComparison.OrdinalIgnoreCase);
        var intent = await _planner.ParseIntentAsync(request.Query, cancellationToken);
        var token = await _mcpToolClient.GetTokenAsync(privileged, cancellationToken);
        var traces = new List<ToolCallResult>
        {
            await _mcpToolClient.CallToolAsync(token, "get_release_status", intent.Component, intent.Version, cancellationToken),
            await _mcpToolClient.CallToolAsync(token, "get_dependencies", intent.Component, intent.Version, cancellationToken),
            await _mcpToolClient.CallToolAsync(token, "check_security_vulnerabilities", intent.Component, intent.Version, cancellationToken)
        };

        var status = ReadString(traces[0].Result, "status") ?? "unknown";
        var vulnerabilityCount = ReadArrayLength(traces[2].Result, "vulnerabilities");

        if (string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase) && vulnerabilityCount == 0)
        {
            traces.Add(await _mcpToolClient.CallToolAsync(token, "approve_release", intent.Component, intent.Version, cancellationToken));
        }

        var verdict = DetermineVerdict(status, vulnerabilityCount, traces.LastOrDefault(trace => trace.Tool == "approve_release"));
        var context = new
        {
            verdict,
            intent,
            approvalMode = request.ApprovalMode,
            status,
            vulnerabilityCount,
            toolCalls = traces
        };
        var synthesis = await _planner.SynthesizeAsync(context, cancellationToken);
        var summary = string.IsNullOrWhiteSpace(synthesis.Summary)
            ? BuildSummary(verdict, intent, status, vulnerabilityCount, traces)
            : synthesis.Summary;
        var recommendation = string.IsNullOrWhiteSpace(synthesis.Recommendation)
            ? BuildRecommendation(verdict)
            : synthesis.Recommendation;

        return new ReviewResult(
            verdict,
            summary,
            recommendation,
            intent,
            request.ApprovalMode,
            _planner.IsConfigured ? $"OpenAI Responses API ({intent.Source})" : "deterministic fallback",
            traces);
    }

    private static string DetermineVerdict(string status, int vulnerabilityCount, ToolCallResult? approvalTrace)
    {
        if (!string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase) || vulnerabilityCount > 0)
        {
            return "blocked";
        }

        if (approvalTrace?.HttpStatus == StatusCodes.Status403Forbidden)
        {
            return "needs_approval_scope";
        }

        if (approvalTrace is { Status: "ok" })
        {
            return "approved";
        }

        return "ready";
    }

    private static string BuildSummary(string verdict, ReleaseIntent intent, string status, int vulnerabilityCount, IReadOnlyList<ToolCallResult> traces)
    {
        var approval = traces.LastOrDefault(trace => trace.Tool == "approve_release");
        return verdict switch
        {
            "blocked" => $"{intent.Component} {intent.Version} is blocked. Release status is {status} and {vulnerabilityCount} vulnerability finding(s) were returned.",
            "needs_approval_scope" => $"{intent.Component} {intent.Version} appears release-ready, but approval was denied because the agent token does not include mcp:tools:release.",
            "approved" => $"{intent.Component} {intent.Version} passed the review and the MCP server returned an approval ticket.",
            _ => $"{intent.Component} {intent.Version} is ready for release review. Approval trace: {approval?.Status ?? "not attempted"}."
        };
    }

    private static string BuildRecommendation(string verdict) => verdict switch
    {
        "blocked" => "Do not release until the blocking status or vulnerability findings are resolved.",
        "needs_approval_scope" => "Run the demo in privileged mode only when you want to demonstrate scoped release approval.",
        "approved" => "Release can proceed in this demo scenario. Preserve the approval ticket in the audit trail.",
        _ => "Review the tool trace before proceeding."
    };

    private static string? ReadString(JsonElement? element, string property)
    {
        if (element is not { } value || !value.TryGetProperty(property, out var propertyValue))
        {
            return null;
        }

        return propertyValue.GetString();
    }

    private static int ReadArrayLength(JsonElement? element, string property)
    {
        if (element is not { } value || !value.TryGetProperty(property, out var propertyValue))
        {
            return 0;
        }

        return propertyValue.ValueKind == JsonValueKind.Array ? propertyValue.GetArrayLength() : 0;
    }
}
