using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using SecureMcpServer.Security;
using SecureMcpServer.Tools;

const string DemoReleaseSecret = "demo-release-secret";
string[] supportedScopes = ["mcp:tools", "mcp:tools:release"];

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<DemoTokenService>();
builder.Services.AddSingleton<ReleaseDataService>();
builder.Services.AddSingleton<SecurityAuditLogger>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "secure-mcp-demo",
            ValidAudience = "mcp-client",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("dev-secret-key-min-32-chars-long-for-demo-only!")),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy => policy.RequireClaim("scope", "mcp:tools"));
    options.AddPolicy("ReleaseAccess", policy => policy.RequireClaim("scope", "mcp:tools:release"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// HTTPS enforcement middleware
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps && !app.Environment.IsDevelopment())
    {
        context.Response.Redirect($"https://{context.Request.Host}{context.Request.Path}");
        return;
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// MCP SSE Endpoint with Auth
app.MapGet("/mcp/sse", async (HttpContext context, DemoTokenService tokenService, SecurityAuditLogger logger) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        logger.LogAuthFailure("SSE", "Missing or invalid Authorization header");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    var token = authHeader.Substring("Bearer ".Length);
    var principal = tokenService.ValidateToken(token);
    
    if (principal == null)
    {
        logger.LogAuthFailure("SSE", "Invalid token");
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    if (!principal.HasClaim("scope", "mcp:tools"))
    {
        logger.LogAuthFailure("SSE", "Insufficient scope");
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Forbidden - missing mcp:tools scope");
        return;
    }

    logger.LogAuthSuccess("SSE", principal.Identity?.Name ?? "unknown");

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    
    var response = context.Response;
    var stream = response.Body;

    // Send initial connection event
    await stream.WriteAsync(Encoding.UTF8.GetBytes("event: endpoint\n"));
    await stream.WriteAsync(Encoding.UTF8.GetBytes("data: /mcp/messages\n\n"));
    await stream.FlushAsync();

    // Keep connection alive
    while (!context.RequestAborted.IsCancellationRequested)
    {
        await Task.Delay(30000, context.RequestAborted);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(": keepalive\n\n"));
        await stream.FlushAsync();
    }
}).RequireAuthorization("McpAccess");

// MCP Messages endpoint
app.MapPost("/mcp/messages", async (HttpContext context, 
    DemoTokenService tokenService,
    ReleaseDataService releaseService,
    SecurityAuditLogger logger) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        return Results.Unauthorized();
    }

    var token = authHeader.Substring("Bearer ".Length);
    var principal = tokenService.ValidateToken(token);
    
    if (principal == null)
    {
        return Results.Unauthorized();
    }

    if (!principal.HasClaim("scope", "mcp:tools"))
    {
        logger.LogAuthFailure("Messages", "Insufficient scope");
        return Results.Json(JsonRpcError(0, 403, "Forbidden - missing mcp:tools scope"), statusCode: StatusCodes.Status403Forbidden);
    }

    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    JsonElement request;
    try
    {
        request = JsonSerializer.Deserialize<JsonElement>(body);
    }
    catch (JsonException ex)
    {
        logger.LogError("messages", ex.Message);
        return Results.Json(JsonRpcError(0, -32700, "Invalid JSON-RPC payload"), statusCode: StatusCodes.Status400BadRequest);
    }
    
    var method = request.GetProperty("method").GetString();
    var id = request.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
    
    logger.LogToolCall(method ?? "unknown", principal.Identity?.Name ?? "unknown");

    object result;
    if (method == "tools/list")
    {
        result = new
        {
            tools = GetToolDefinitions()
        };
    }
    else if (method == "tools/call")
    {
        result = await HandleToolCall(request, principal, releaseService, logger);
    }
    else
    {
        return Results.Json(JsonRpcError(id, -32601, "Unknown method"), statusCode: StatusCodes.Status400BadRequest);
    }

    if (result is ToolCallFailure failure)
    {
        return Results.Json(JsonRpcError(id, failure.Code, failure.Message), statusCode: failure.StatusCode);
    }

    return Results.Json(new { jsonrpc = "2.0", id, result });
}).RequireAuthorization("McpAccess");

async Task<object> HandleToolCall(JsonElement request, ClaimsPrincipal principal, ReleaseDataService releaseService, SecurityAuditLogger logger)
{
    var toolName = request.GetProperty("params").GetProperty("name").GetString();
    var args = request.GetProperty("params").GetProperty("arguments");

    try
    {
        return toolName switch
        {
            "get_release_status" => releaseService.GetReleaseStatus(
                args.GetProperty("component").GetString()!,
                args.GetProperty("version").GetString()!),
            "get_dependencies" => releaseService.GetDependencies(
                args.GetProperty("component").GetString()!,
                args.GetProperty("version").GetString()!),
            "check_security_vulnerabilities" => releaseService.CheckVulnerabilities(
                args.GetProperty("component").GetString()!,
                args.GetProperty("version").GetString()!),
            "approve_release" when principal.HasClaim("scope", "mcp:tools:release") =>
                await releaseService.ApproveReleaseAsync(
                    args.GetProperty("component").GetString()!,
                    args.GetProperty("version").GetString()!,
                    principal),
            "approve_release" => new ToolCallFailure(
                StatusCodes.Status403Forbidden,
                403,
                "Release approval requires mcp:tools:release scope"),
            _ => new ToolCallFailure(
                StatusCodes.Status400BadRequest,
                -32602,
                $"Unknown tool: {toolName}")
        };
    }
    catch (Exception ex)
    {
        logger.LogError(toolName!, ex.Message);
        return new { error = ex.Message };
    }
}

// Token issuance endpoint (dev only)
app.MapPost("/auth/token", (DemoTokenService tokenService, [FromBody] TokenRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.ClientId) || req.Scopes.Length == 0)
    {
        return Results.BadRequest(new { error = "ClientId and at least one scope are required" });
    }

    var requestedScopes = req.Scopes.Distinct(StringComparer.Ordinal).ToArray();
    var unknownScopes = requestedScopes.Except(supportedScopes, StringComparer.Ordinal).ToArray();
    if (unknownScopes.Length > 0)
    {
        return Results.BadRequest(new { error = $"Unsupported scope: {string.Join(", ", unknownScopes)}" });
    }

    if (requestedScopes.Contains("mcp:tools:release") && req.ClientSecret != DemoReleaseSecret)
    {
        return Results.Json(
            new { error = "mcp:tools:release requires the demo release client secret" },
            statusCode: StatusCodes.Status403Forbidden);
    }

    var token = tokenService.GenerateToken(req.ClientId, requestedScopes);
    return Results.Ok(new { access_token = token, token_type = "Bearer" });
});

// === MCP Tool Discovery Endpoint (added for better agent flow) ===
app.MapGet("/mcp/tools", (ClaimsPrincipal? user) =>
{
    if (user == null || !user.HasClaim("scope", "mcp:tools"))
    {
        return Results.Json(new { error = "Unauthorized - mcp:tools scope required" }, statusCode: 401);
    }

    return Results.Json(new { tools = GetToolDefinitions() });
}).RequireAuthorization("McpAccess");

app.Run();

object[] GetToolDefinitions() =>
[
    new
    {
        name = "get_release_status",
        description = "Get release status for a component version",
        inputSchema = ComponentVersionSchema()
    },
    new
    {
        name = "get_dependencies",
        description = "List dependencies for a component version",
        inputSchema = ComponentVersionSchema()
    },
    new
    {
        name = "check_security_vulnerabilities",
        description = "Check known vulnerabilities for a component version",
        inputSchema = ComponentVersionSchema()
    },
    new
    {
        name = "approve_release",
        description = "Approve release after security review (requires mcp:tools:release)",
        inputSchema = ComponentVersionSchema()
    }
];

object ComponentVersionSchema() => new
{
    type = "object",
    properties = new
    {
        component = new { type = "string" },
        version = new { type = "string" }
    },
    required = new[] { "component", "version" }
};

object JsonRpcError(int id, int code, string message) => new
{
    jsonrpc = "2.0",
    id,
    error = new { code, message }
};

record TokenRequest(string ClientId, string[] Scopes, string? ClientSecret = null);
record ToolCallFailure(int StatusCode, int Code, string Message);

public partial class Program;
