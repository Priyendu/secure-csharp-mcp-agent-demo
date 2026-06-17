using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SecureMcpShared.Models;

namespace SecureMcpAgentWeb.Services;

public sealed class OpenAiReleasePlanner
{
    private readonly HttpClient _httpClient;
    private readonly AgentOptions _options;
    private readonly ReleaseIntentParser _fallbackParser;

    public OpenAiReleasePlanner(HttpClient httpClient, IOptions<AgentOptions> options, ReleaseIntentParser fallbackParser)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fallbackParser = fallbackParser;
        _httpClient.BaseAddress = new Uri("https://api.openai.com");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAiApiKey);

    public async Task<ReleaseIntent> ParseIntentAsync(string query, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return _fallbackParser.Parse(query);
        }

        try
        {
            var text = await CreateStructuredResponseAsync(new
            {
                name = "release_intent",
                schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        component = new { type = "string" },
                        version = new { type = "string" },
                        intent = new { type = "string", @enum = new[] { "review_release" } },
                        confidence = new { type = "number" }
                    },
                    required = new[] { "component", "version", "intent", "confidence" }
                }
            },
            "Extract the release component and version from the user's request. Return Unknown and 1.0.0 if missing.",
            query,
            cancellationToken);

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return new ReleaseIntent(
                root.GetProperty("component").GetString() ?? "Unknown",
                root.GetProperty("version").GetString() ?? "1.0.0",
                root.GetProperty("intent").GetString() ?? "review_release",
                root.GetProperty("confidence").GetDouble(),
                "openai");
        }
        catch
        {
            var fallback = _fallbackParser.Parse(query);
            return fallback with { Source = "deterministic-after-openai-error" };
        }
    }

    public async Task<LlmSynthesis> SynthesizeAsync(object reviewContext, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new LlmSynthesis("", "");
        }

        try
        {
            var text = await CreateStructuredResponseAsync(new
            {
                name = "release_review_summary",
                schema = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        summary = new { type = "string" },
                        recommendation = new { type = "string" }
                    },
                    required = new[] { "summary", "recommendation" }
                }
            },
            "Write a concise security release-review summary. Respect the deterministic verdict and do not claim approval unless the tool trace shows approval succeeded.",
            JsonSerializer.Serialize(reviewContext),
            cancellationToken);

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return new LlmSynthesis(
                root.GetProperty("summary").GetString() ?? "",
                root.GetProperty("recommendation").GetString() ?? "");
        }
        catch
        {
            return new LlmSynthesis("", "");
        }
    }

    private async Task<string> CreateStructuredResponseAsync(object format, string instructions, string input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.OpenAiModel,
            instructions,
            input,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = format.GetType().GetProperty("name")?.GetValue(format),
                    strict = true,
                    schema = format.GetType().GetProperty("schema")?.GetValue(format)
                }
            }
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ExtractOutputText(document.RootElement)
            ?? throw new InvalidOperationException("OpenAI response did not contain output text.");
    }

    private static string? ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output))
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content))
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}
