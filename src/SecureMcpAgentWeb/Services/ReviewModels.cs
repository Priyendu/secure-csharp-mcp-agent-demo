using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecureMcpAgentWeb.Services;

public sealed record ReleaseReviewRequest(string Query, string ApprovalMode = "standard");

public sealed record ReleaseIntent(
    string Component,
    string Version,
    string Intent,
    double Confidence,
    string Source);

public sealed record ToolTrace(
    string Tool,
    string Status,
    int HttpStatus,
    JsonElement? Result,
    JsonElement? Error);

public sealed record ReleaseReviewResult(
    string Verdict,
    string Summary,
    string Recommendation,
    ReleaseIntent Intent,
    string ApprovalMode,
    string LlmMode,
    IReadOnlyList<ToolTrace> ToolCalls);

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType);

public sealed record LlmSynthesis(string Summary, string Recommendation);
