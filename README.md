# Secure C# MCP Agent Demo

Secure MCP (Model Context Protocol) server, console client, web UI, and LLM-capable release-review agent using .NET 8, JWT authentication, scope-based authorization, audit logging, demo release data, and OpenAI Responses API integration.

This repository is intentionally educational. It demonstrates secure patterns and negative-path checks, but it is not a production identity or release-approval system.

## Features

- ASP.NET Core MCP-style server over HTTPS + SSE.
- JWT bearer authentication with issuer, audience, lifetime, and signing-key validation.
- Scope-based authorization with `mcp:tools` and `mcp:tools:release`.
- Four implemented tools:
  - `get_release_status`
  - `get_dependencies`
  - `check_security_vulnerabilities`
  - `approve_release`
- `/mcp/tools` and JSON-RPC `tools/list` return the same tool definitions.
- Demo release data in `src/SecureMcpServer/demo-data.json`.
- Security audit logging for auth events, tool calls, and tool errors.
- End-to-end web UI for release review demos.
- **New: Full native C# WPF desktop application** (`SecureMcpDesktopDemo`) that provides a self-contained E2E experience — start the secure server from the UI, acquire tokens, run the exact review scenarios, view live traces, protocol logs, and manually exercise every tool and authorization boundary.
- Agent API that can use OpenAI for intent parsing and summary generation.
- Deterministic fallback mode when no `OPENAI_API_KEY` is configured.
- Negative auth and tool-flow tests for 401/403 behavior.
- GitHub Actions workflow for restore, build, and test.

## Quick Start

```bash
dotnet restore
dotnet build
cd src/SecureMcpServer
dotnet run --launch-profile https

# In another terminal
cd ../SecureMcpClient
dotnet run -- "Can we release AnalyzerService version 2.4.1?"
```

The client requests only the `mcp:tools` scope, so the final `approve_release` call is expected to fail with `403 Forbidden`. That is the demo's negative authorization path.

## Web UI + LLM Demo

Start the MCP server:

```bash
cd src/SecureMcpServer
dotnet run --launch-profile https
```

Start the web agent:

```bash
cd src/SecureMcpAgentWeb
dotnet run --urls http://localhost:5080
```

Open `http://localhost:5080`.

Optional LLM configuration:

```bash
set OPENAI_API_KEY=your-key
set OPENAI_MODEL=gpt-4o-mini
```

If `OPENAI_API_KEY` is omitted, the web agent still runs with deterministic parsing and summaries.

## Native C# WPF Desktop Demo (Best for End-to-End Experience)

This is a **complete, self-contained C# UI** that demonstrates every security and MCP concept from one native Windows application:

- One-click start/stop of the real SecureMcpServer (launches as child process on https://localhost:5001)
- Acquire **Standard** (`mcp:tools`) or **Privileged** (`mcp:tools + mcp:tools:release`) tokens directly from the UI
- Three canonical scenarios + free-form queries + manual tool explorer
- Live tool trace with HTTP status highlighting (green success, red 403 for the famous negative path)
- Protocol log (every JSON-RPC request/response) + captured server audit output
- Local deterministic intent parser (same logic as the web agent)
- JWT claim decoder so you can see the scopes the server will enforce
- Full step-by-step mode so you can pause and inspect each authorization decision

**Run it:**

```bash
dotnet build
# Then run the WPF project (from Visual Studio, or):
dotnet run --project src/SecureMcpDesktopDemo
```

Inside the app:
1. Click **Start Secure MCP Server**
2. Click one of the three scenario buttons (or type a query and choose Standard/Privileged)
3. Watch the trace, verdict, and raw responses update in real time
4. Use the Manual Tool Call section to experiment with any token + any tool

The desktop app makes the authorization boundary extremely visible: run "Ready" with a standard token and you will see the `approve_release` call return 403. Switch to Privileged and the same call succeeds.

## Demo Token Rules

`POST /auth/token` is a demo-only token issuer.

- Ordinary tool access can request `mcp:tools`.
- Release approval requires `mcp:tools:release`.
- The privileged release scope is rejected unless the request includes the demo-only `ClientSecret` value `demo-release-secret`.
- Unknown scopes are rejected.

Example privileged request:

```json
{
  "clientId": "demo-client",
  "scopes": [ "mcp:tools", "mcp:tools:release" ],
  "clientSecret": "demo-release-secret"
}
```

## Verification

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

The test suite covers:

- release-scope token rejection without the demo secret
- `approve_release` rejection without `mcp:tools:release`
- successful release approval with the privileged demo token
- tool discovery consistency between `/mcp/tools` and `tools/list`
- demo JSON data being used by tool responses
- client planner parsing behavior
- web agent parser behavior
- web UI static smoke test

### Testing the Architecture Diagrams & Generator

After editing `tools/generate_architecture_assets.py` or the docs:

```powershell
python tools/generate_architecture_assets.py
```

- Open `docs/architecture-diagrams.drawio` in https://app.diagrams.net (or VS Code with Draw.io extension).
- Review the updated `docs/ARCHITECTURE.pdf` (it includes new cards for Desktop Demo and Shared Models).
- The script updates both the editable diagram and the PDF.

### Building and Manually Testing the Desktop Demo

```powershell
dotnet build --configuration Release
dotnet run --project src/SecureMcpDesktopDemo
```

Inside the WPF app:

1. Click **Start Secure MCP Server** (it launches the real server as a child process).
2. Use the scenario buttons (Ready / Approve / Blocked) or enter a custom query.
3. Switch between Standard and Privileged tokens to see 403 vs. success on `approve_release`.
4. Try the Manual Tool Call panel.
5. Check the Protocol Log and Server Log tabs.

First-time HTTPS note (Windows): Run `dotnet dev-certs https --trust` once in an elevated prompt if you see certificate warnings.

The Desktop project also builds as part of the full solution: `dotnet build --configuration Release`.

## Folder Structure

```text
secure-csharp-mcp-agent-demo/
|-- src/
|   |-- SecureMcpServer/          # MCP server (JWT, scopes, 4 tools)
|   |-- SecureMcpClient/          # Simple console client
|   |-- SecureMcpAgentWeb/        # Web UI + agent (with optional OpenAI)
|   |-- SecureMcpDesktopDemo/     # Native WPF C# desktop E2E demo (best for interactive demos)
|   `-- SecureMcpShared/          # Shared models (ReleaseIntent, ToolCallResult, ReviewResult, TokenResponse)
|-- tests/
|-- docs/                         # Architecture docs + diagrams
|-- tools/                        # generate_architecture_assets.py
|-- .github/workflows/
|-- DEMO.md
|-- README.md
`-- SECURITY.md
```

**Desktop Demo note:** `SecureMcpDesktopDemo` is a complete C# WPF app that can start the server, acquire tokens, run scenarios, show live traces/logs, and manually call tools — all demonstrating the full security model in one native application. It shares models via `SecureMcpShared` with the web agent.

## Production Hardening Notes

Before adapting this pattern for real systems:

- replace the demo token endpoint with OAuth2/OIDC
- move secrets to a managed secret store
- use asymmetric JWT signing with key rotation
- add rate limiting and stricter request validation
- send audit logs to a tamper-resistant sink
- enforce TLS and certificate management outside local development

MIT License - for educational/demo use only.
