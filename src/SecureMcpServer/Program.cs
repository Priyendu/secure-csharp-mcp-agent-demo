using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
// using SecureMcpServer.Mcp; // commented - MCP namespace not separate
using SecureMcpServer.Security;
using SecureMcpServer.Tools;

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
    await stream.WriteAsync(Encoding.UTF8.GetBytes($"data: /mcp/messages?token={token}\n\n"));
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
    
    if (principal == null || !principal.HasClaim("scope", "mcp:tools"))
    {
        return Results.Unauthorized();
    }

    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<JsonElement>(body);
    
    var method = request.GetProperty("method").GetString();
    var id = request.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
    
    logger.LogToolCall(method ?? "unknown", principal.Identity?.Name ?? "unknown");

    object result = method switch
    {
        "tools/list" => new
        {
            tools = new object[]
            {
                new { name = "get_release_status", description = "Get release status for a component" },
                new { name = "get_dependencies", description = "List dependencies for a component version" },
                new { name = "check_security_vulnerabilities", description = "Check for known CVEs" },
                new { name = "approve_release", description = "Approve release (requires release scope)" }
            }
        },
        "tools/call" => await HandleToolCall(request, principal, releaseService, logger),
        _ => new { error = "Unknown method" }
    };

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
            "approve_release" => await releaseService.ApproveReleaseAsync(
                args.GetProperty("component").GetString()!,
                args.GetProperty("version").GetString()!,
                principal),
            _ => new { error = "Unknown tool" }
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
    var token = tokenService.GenerateToken(req.ClientId, req.Scopes);
    return Results.Ok(new { access_token = token, token_type = "Bearer" });
});

// === MCP Tool Discovery Endpoint (added for better agent flow) ===
app.MapGet("/mcp/tools", (ClaimsPrincipal? user) =>
{
    if (user == null || !user.HasClaim("scope", "mcp:tools"))
    {
        return Results.Json(new { error = "Unauthorized - mcp:tools scope required" }, statusCode: 401);
    }

    var tools = new object[]
    {
        new
        {
            name = "get_component_security_profile",
            description = "Return security profile for a component",
            inputSchema = new { type = "object", properties = new { componentName = new { type = "string" } }, required = new[] { "componentName" } }
        },
        new
        {
            name = "check_known_vulnerabilities",
            description = "Return known vulnerability findings for a component and version",
            inputSchema = new { type = "object", properties = new { componentName = new { type = "string" }, version = new { type = "string" } }, required = new[] { "componentName", "version" } }
        },
        new
        {
            name = "generate_security_assessment",
            description = "Generate deterministic risk assessment",
            inputSchema = new { type = "object", properties = new { componentName = new { type = "string" }, criticality = new { type = "string" }, networkExposure = new { type = "string" }, handlesSensitiveData = new { type = "boolean" }, findingCount = new { type = "integer" }, highestSeverity = new { type = "string" } }, required = new[] { "componentName" } }
        },
        new
        {
            name = "create_release_security_summary",
            description = "Generate release recommendation based on profile + vulnerabilities",
            inputSchema = new { type = "object", properties = new { componentName = new { type = "string" }, releaseVersion = new { type = "string" } }, required = new[] { "componentName", "releaseVersion" } }
        }
    };

    return Results.Json(new { tools });
}).RequireAuthorization("McpAccess");

app.Run();

record TokenRequest(string ClientId, string[] Scopes);
