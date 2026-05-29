namespace SecureMcpAgentWeb.Services;

public sealed class AgentOptions
{
    public string McpServerUrl { get; set; } = "https://localhost:5001";
    public string? OpenAiApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string DemoReleaseClientSecret { get; set; } = "demo-release-secret";
    public bool AllowInvalidDevCertificates { get; set; } = true;
}
