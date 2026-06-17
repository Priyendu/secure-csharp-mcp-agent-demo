namespace SecureMcpAgentWeb.Services;

/// <summary>
/// Web-specific request DTO for the /api/review endpoint (the web UI contract).
/// The core domain models (ReleaseIntent, ToolCallResult, ReviewResult, TokenResponse)
/// have been moved to SecureMcpShared.Models so they can be reused by the desktop demo
/// and other clients without duplication.
/// </summary>
public sealed record ReleaseReviewRequest(string Query, string ApprovalMode = "standard");

/// <summary>
/// Result of an LLM (or fallback) synthesis step. Internal to the web agent's OpenAI path.
/// </summary>
public sealed record LlmSynthesis(string Summary, string Recommendation);
