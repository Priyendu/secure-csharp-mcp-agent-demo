using SecureMcpAgentWeb.Services;
using SecureMcpShared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(options =>
{
    options.McpServerUrl = builder.Configuration["MCP_SERVER_URL"] ?? "https://localhost:5001";
    options.OpenAiApiKey = builder.Configuration["OPENAI_API_KEY"];
    options.OpenAiModel = builder.Configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";
    options.DemoReleaseClientSecret = builder.Configuration["DEMO_RELEASE_CLIENT_SECRET"] ?? "demo-release-secret";
    options.AllowInvalidDevCertificates = bool.TryParse(builder.Configuration["ALLOW_INVALID_DEV_CERTIFICATES"], out var allow)
        ? allow
        : builder.Environment.IsDevelopment();
});

builder.Services.AddHttpClient<McpToolClient>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = options.AllowInvalidDevCertificates
                ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                : null
        };
    });
builder.Services.AddHttpClient<OpenAiReleasePlanner>();
builder.Services.AddSingleton<ReleaseIntentParser>();
builder.Services.AddScoped<ReleaseReviewOrchestrator>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", (Microsoft.Extensions.Options.IOptions<AgentOptions> options) =>
{
    var value = options.Value;
    return Results.Ok(new
    {
        mcpServerUrl = value.McpServerUrl,
        llmMode = string.IsNullOrWhiteSpace(value.OpenAiApiKey) ? "deterministic fallback" : $"OpenAI Responses API ({value.OpenAiModel})"
    });
});

app.MapPost("/api/review", async (ReleaseReviewRequest request, ReleaseReviewOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
    {
        return Results.BadRequest(new { error = "Query is required" });
    }

    var result = await orchestrator.ReviewAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.Run();

public partial class Program;
