using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecureMcpShared.Models;

/// <summary>
/// Represents a parsed release review intent (component + version).
/// Used by agent-side planners (deterministic or LLM) and passed to the MCP server.
/// </summary>
public sealed record ReleaseIntent(
    string Component,
    string Version,
    string Intent,
    double Confidence,
    string Source);

/// <summary>
/// Rich result of a single MCP tool call (JSON-RPC over the authenticated channel).
/// Captures status, the parsed result/error, HTTP code, and the raw wire response
/// for auditing and UI display.
/// 
/// This is the canonical shared representation used by the web agent, desktop demo,
/// and any other clients that want detailed traces.
/// </summary>
public sealed record ToolCallResult(
    string Tool,
    int HttpStatus,
    string Status,
    JsonElement? Result,
    JsonElement? Error,
    string Raw);

/// <summary>
/// DTO for the response from the demo /auth/token endpoint.
/// </summary>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType);

/// <summary>
/// High-level result of an orchestrated release review.
/// Contains the verdict, human-readable summary/recommendation, the original intent,
/// which approval mode was used, LLM/deterministic mode info, and the full ordered
/// list of tool calls that were made (including any 403s that demonstrate the
/// scope-based authorization boundary).
/// </summary>
public sealed record ReviewResult(
    string Verdict,
    string Summary,
    string Recommendation,
    ReleaseIntent Intent,
    string ApprovalMode,
    string LlmMode,
    IReadOnlyList<ToolCallResult> ToolCalls);
