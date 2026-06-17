# Secure C# MCP Agent Demo - Architecture

Version: 1.4
Audience: Security engineers and .NET architects  
Date: June 2026  
Status: Demo / educational

## Executive Summary

This project demonstrates a secure-by-default shape for a small MCP-style release-assessment server, a rule-based console client, an end-to-end web release-review agent, and a native C# WPF desktop demo application. It is built with .NET 8 and focuses on auth boundaries, scope checks, consistent tool discovery, auditable tool calls, optional LLM-assisted reasoning, negative-path tests, and shared data models across multiple clients.

A new `SecureMcpShared` class library provides canonical models (`ReleaseIntent`, `ToolCallResult`, `ReviewResult`, `TokenResponse`) so that the web agent and desktop demo (and future clients) share the same contracts without duplication.

The system is not production-ready identity infrastructure. The token issuer and secrets are intentionally local demo mechanisms.

## Diagram Artifacts

- `docs/architecture-diagrams.drawio` contains the editable Draw.io source with system overview, release review sequence, and authorization boundary diagrams (focused on the core MCP server + web agent + LLM flow).
- `docs/ARCHITECTURE.pdf` is the polished PDF version generated from the same architecture model.
- `tools/generate_architecture_assets.py` regenerates both diagram artifacts.

**Note:** The visual diagrams pre-date the addition of `SecureMcpDesktopDemo` and `SecureMcpShared`. They continue to accurately represent the core authorization model, tool contract, and server behavior. The desktop demo and shared library are additional consumers of the same MCP surface and data models. Update the Draw.io source manually or extend the generator script if a full visual refresh is required.

## Components

| Component | Responsibility | Security control |
| --- | --- | --- |
| `SecureMcpServer` | ASP.NET Core Minimal API host for auth, SSE, JSON-RPC messages, and tool discovery | JWT bearer auth and scope policies |
| `DemoTokenService` | Issues and validates demo JWTs | issuer, audience, signing key, lifetime validation |
| `ReleaseDataService` | Serves release status, dependencies, vulnerability data, and release approval | checks `mcp:tools:release` before approval |
| `SecurityAuditLogger` | Logs auth success/failure, tool calls, and tool errors | structured application logs |
| `SecureMcpClient` | Console client with a simple rule-based planner | obtains token and delegates auth decisions to the server |
| `SecureMcpAgentWeb` | Browser UI and agent API for release review demos | calls MCP tools through JWT-scoped requests |
| `SecureMcpDesktopDemo` | Native WPF desktop application providing rich end-to-end demo experience (scenarios, token lab, live traces, protocol logs, manual tool explorer). Can launch the MCP server as a child process. | re-uses the same JWT + scoped MCP client contract |
| `SecureMcpShared` | Small class library providing shared models (`ReleaseIntent`, `ToolCallResult`, `ReviewResult`, `TokenResponse`) | ensures consistent contracts between web agent and desktop (and future clients) |
| `OpenAiReleasePlanner` | Optional OpenAI Responses API client for intent parsing and summary generation | no direct release authority; falls back deterministically |
| `demo-data.json` | Self-contained release data for deterministic responses | copied to build output |

## Shared Models and Desktop Client

`SecureMcpShared` is a small, dependency-light class library that defines the core data contracts used by agent-style clients:

- `ReleaseIntent` — parsed component + version (with confidence and source)
- `ToolCallResult` — tool name, HTTP status, status string, JSON result/error, and the raw wire response (for auditing and UI display)
- `ReviewResult` — full orchestrated outcome (verdict, summary, recommendation, intent, approval mode, LLM mode, list of `ToolCallResult`)
- `TokenResponse` — the shape returned by the demo `/auth/token` endpoint

Both `SecureMcpAgentWeb` (via its `McpToolClient` and `ReleaseReviewOrchestrator`) and `SecureMcpDesktopDemo` (via `McpDesktopClient` and `DesktopReviewOrchestrator`) now depend on `SecureMcpShared`. This eliminates duplication and guarantees that every client sees identical shapes for intent, tool traces, and review outcomes.

`SecureMcpDesktopDemo` is a .NET 8 WPF application that provides a complete, self-contained end-to-end demonstration experience:
- One-click launch/stop of the real `SecureMcpServer` (as a child process on the conventional demo port)
- Token acquisition lab (standard vs. privileged with the demo secret)
- Scenario buttons matching the original demo (Ready → 403, Approve → success, Blocked)
- Live updating tool trace with color-coded HTTP statuses
- Protocol log (every JSON-RPC request/response) and captured server audit output
- Manual tool call explorer for arbitrary experimentation
- JWT claim decoder to make scopes visible
- Deterministic local parser and orchestration (no external LLM required)

The desktop app deliberately re-uses the exact same MCP contract and shared models so that any new security or protocol change is automatically visible across all clients.

## End-to-End Flow

Multiple clients exercise the same secure MCP contract:

- Console client (`SecureMcpClient`)
- Web agent + browser UI (`SecureMcpAgentWeb`)
- Native C# WPF desktop demo (`SecureMcpDesktopDemo`) — can optionally launch the server process for a true single-application E2E experience

```text
Client (Browser UI / Desktop WPF / Console)
  |
  | POST /auth/token
  | POST /mcp/messages tools/call  (or direct in desktop)
  v
SecureMcpServer
  |
  | get_release_status
  | get_dependencies
  | check_security_vulnerabilities
  | approve_release when eligible (enforces mcp:tools:release)
  v
Client
  |
  | deterministic (or LLM-assisted) verdict + tool trace + raw responses
```

The (optional) planner in the web agent or desktop only parses intent and synthesizes narrative. All authorization decisions and data come from the MCP server. The desktop demo also includes a built-in `ServerLauncher` that can start `SecureMcpServer` as a child process on the conventional demo port.

## Release Review Sequence

```text
Client (Browser / Desktop WPF)   Agent/Orchestrator   Planner/LLM   MCP Server   Release Tools
    |                              |                   |             |            |
    | run review (query + mode)    |                   |             |            |
    |----------------------------->|                   |             |            |
    |                              | parse intent      |             |            |
    |                              |------------------>|             |            |
    |                              | structured intent |             |            |
    |                              |<------------------|             |            |
    |                              | request JWT + tool calls        |            |
    |                              |-------------------------------->|            |
    |                              |                               | execute      |
    |                              |                               |------------->|
    |                              | tool trace + auth result      |<-------------|
    |                              |<--------------------------------|            |
    | verdict + trace + raw JSON   |                   |             |            |
    |<-----------------------------|                   |             |            |
```

The same sequence is driven by the native WPF `SecureMcpDesktopDemo` (via `DesktopReviewOrchestrator` + `McpDesktopClient`) and the web agent. Approval is attempted only when status is `ready` and no vulnerability findings are returned. Whether approval succeeds is determined by the MCP server's `mcp:tools:release` scope check.

The desktop client can also launch the server locally via its `ServerLauncher` for a complete single-process demo experience.

## Endpoint Model

| Endpoint | Method | Purpose | Auth |
| --- | --- | --- | --- |
| `/auth/token` | POST | Demo-only JWT issuance | validates requested scopes and demo release secret |
| `/mcp/sse` | GET | SSE endpoint advertisement | requires `mcp:tools` |
| `/mcp/messages` | POST | JSON-RPC `tools/list` and `tools/call` | requires `mcp:tools`; `approve_release` also requires `mcp:tools:release` |
| `/mcp/tools` | GET | Declarative tool discovery | requires `mcp:tools` |
| `/api/config` | GET | Web agent runtime mode | none |
| `/api/review` | POST | End-to-end release review orchestration | calls MCP server with scoped JWT |

The SSE endpoint advertises `/mcp/messages` without embedding the bearer token in the URL. Clients should continue to send the token in the `Authorization` header.

## Tool Contract

`/mcp/tools` and `tools/list` expose the same four implemented tools:

| Tool | Purpose | Required arguments | Additional scope |
| --- | --- | --- | --- |
| `get_release_status` | Returns release readiness and approvers | `component`, `version` | none |
| `get_dependencies` | Returns component dependencies | `component`, `version` | none |
| `check_security_vulnerabilities` | Returns demo vulnerability findings | `component`, `version` | none |
| `approve_release` | Approves a release and returns an approval ticket | `component`, `version` | `mcp:tools:release` |

## Auth Flow

```text
Client
  |
  | POST /auth/token { clientId, scopes, clientSecret? }
  v
SecureMcpServer
  |
  | validate requested scopes
  | require demo-release-secret for mcp:tools:release
  v
JWT access token

Client
  |
  | Authorization: Bearer <token>
  | POST /mcp/messages tools/call
  v
SecureMcpServer
  |
  | validate JWT
  | require mcp:tools
  | require mcp:tools:release for approve_release
  | audit call
  v
JSON-RPC result or JSON-RPC error
```

Negative paths:

- missing or invalid token returns 401
- valid token without `mcp:tools` returns 403
- `approve_release` without `mcp:tools:release` returns 403
- privileged token issuance without the demo release secret returns 403
- unknown scopes return 400

## Data Flow

`ReleaseDataService` loads `demo-data.json` from the server output directory. If the file is not present, it falls back to an embedded default payload so the demo remains runnable during local experiments.

Current demo releases:

- `AnalyzerService` `2.4.1`: ready, no vulnerabilities
- `PaymentGateway` `5.8.0`: blocked, includes `CVE-DEMO-2026-0001`

## Testing Strategy

The server tests use `WebApplicationFactory<Program>` to exercise the actual ASP.NET Core pipeline. Coverage includes token issuance constraints, release-scope authorization, tool discovery consistency, and demo data responses.

The client tests cover the rule-based planner's known and fallback parsing paths. The web-agent tests cover the deterministic parser and static UI smoke path.

The desktop demo is exercised manually via its rich UI (scenarios, manual calls, token modes, live traces) but shares the same model and client logic as the tested web agent through `SecureMcpShared`.

Run:

```bash
dotnet test --configuration Release
```

For the desktop experience:

```bash
dotnet run --project src/SecureMcpDesktopDemo
```

(Click "Start Secure MCP Server" inside the app, then use the scenario buttons or manual explorer.)

## Production Hardening

For real deployment:

1. Replace `/auth/token` with OAuth2/OIDC through a trusted provider.
2. Replace symmetric demo signing with RSA or ECDSA and key rotation.
3. Store secrets in a managed secret store.
4. Add rate limiting, request-size limits, and stricter JSON-RPC validation.
5. Store audit logs in a tamper-resistant sink.
6. Add end-to-end tests for the intended MCP transport and client behavior.
7. Replace demo fallback parsing with a reviewed production planner if this is adapted outside a demo.

This architecture document is for educational and demonstration purposes only.
