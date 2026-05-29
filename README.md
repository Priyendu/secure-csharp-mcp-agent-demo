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

```bash
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

## Folder Structure

```text
secure-csharp-mcp-agent-demo/
|-- src/
|   |-- SecureMcpServer/
|   |   |-- Program.cs
|   |   |-- demo-data.json
|   |   |-- Security/
|   |   `-- Tools/
|   |-- SecureMcpClient/
|   `-- SecureMcpAgentWeb/
|-- tests/
|-- docs/
|-- .github/workflows/
|-- DEMO.md
|-- README.md
`-- SECURITY.md
```

## Production Hardening Notes

Before adapting this pattern for real systems:

- replace the demo token endpoint with OAuth2/OIDC
- move secrets to a managed secret store
- use asymmetric JWT signing with key rotation
- add rate limiting and stricter request validation
- send audit logs to a tamper-resistant sink
- enforce TLS and certificate management outside local development

MIT License - for educational/demo use only.
