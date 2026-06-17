# PR: Add native C# WPF end-to-end desktop demo + extract shared models into `SecureMcpShared`

## Summary

This PR delivers two closely related improvements that together provide a complete, self-contained, and consistent way to experience the secure MCP demo:

1. **New `SecureMcpDesktopDemo`** — a rich native WPF (.NET 8) desktop application that serves as a first-class end-to-end demo client.
2. **New `SecureMcpShared`** class library — canonical shared models extracted from the web agent so that all clients (web + desktop + future) use identical data contracts.

The desktop app makes the authorization boundaries, token rules, tool contract, negative paths, and audit story extremely visible and interactive — all from a single C# Windows application that can even launch the MCP server itself.

## Motivation

- The original web UI (HTML/JS + ASP.NET agent) is excellent for browser-based demos, but many .NET developers and security reviewers prefer (or need) a pure C# / native desktop experience.
- Several data models (`ReleaseIntent`, tool trace types, review results, token DTOs) were duplicated between the web agent and the new desktop code. Centralizing them prevents drift and makes future clients trivial to add.

## Key Changes

### New Projects

- **`src/SecureMcpDesktopDemo`** (WPF)
  - One-click Start/Stop for the real `SecureMcpServer` (child process on `https://localhost:5001`)
  - Three canonical scenario buttons ("Ready", "Approve", "Blocked") exactly matching the original demo expectations
  - Free-form query + Standard vs Privileged token mode
  - Live tool trace with color-coded HTTP status (green success, red 403 for the famous negative authorization path)
  - Protocol log (every JSON-RPC request/response) + captured server stdout (auth events, tool calls)
  - Manual tool explorer (call any tool with any token/component/version)
  - JWT claim decoder (makes scopes visible to the user)
  - Local deterministic parser + orchestrator (same logic as web agent, no LLM required)
  - Fully self-contained: run `dotnet run --project src/SecureMcpDesktopDemo` and explore everything from one app

- **`src/SecureMcpShared`** (class library)
  - `ReleaseIntent`
  - `ToolCallResult` (now includes `Raw` response for excellent trace/audit UX)
  - `ReviewResult`
  - `TokenResponse`
  - Both `SecureMcpAgentWeb` and `SecureMcpDesktopDemo` now reference this library

### Refactoring

- Web agent services (`McpToolClient`, `ReleaseReviewOrchestrator`, parsers, etc.) were updated to use the shared types (`ToolTrace` → `ToolCallResult`, `ReleaseReviewResult` → `ReviewResult`).
- Local duplicate record definitions removed from the desktop project.
- `ReviewModels.cs` in the web project now only contains the web-specific `ReleaseReviewRequest` and `LlmSynthesis`.

### Documentation

- Updated `docs/ARCHITECTURE.md` (v1.4) with new components, flows, and explanations for the desktop demo and shared models.
- Updated `README.md` and `DEMO.md` (in earlier commits on this lineage) with desktop usage instructions.

## How the Demo Demonstrates All Concepts

From the desktop UI you can directly observe:

- JWT bearer authentication + scope-based authorization (`mcp:tools` vs `mcp:tools:release`)
- The demo-only token issuer and the requirement for `demo-release-secret` on privileged scope
- Clear 403 negative paths (standard token → `approve_release` is rejected)
- The four MCP tools and consistent discovery (`/mcp/tools` and `tools/list`)
- Same tool contract used by console client, web agent, *and* this native client
- Local deterministic "agent" parsing vs. server-enforced policy and data (`demo-data.json`)
- Full audit visibility (client protocol log + server-side auth/tool logs)
- End-to-end release review orchestration with verdicts: `approved`, `needs_approval_scope`, `blocked`

## Testing & Verification

- Full solution builds cleanly (`dotnet build SecureMcpAgentDemo.sln`)
- All existing tests pass (`dotnet test`)
- Desktop app was manually exercised with all three scenarios + manual tool calls + token modes
- Architecture document updated and versioned

## How to Review / Run

```powershell
# Clone and checkout the branch
git clone https://github.com/Priyendu/secure-csharp-mcp-agent-demo.git
cd secure-csharp-mcp-agent-demo
git checkout feature/extract-shared-models

# Build & test
dotnet build
dotnet test

# Best experience: run the desktop demo
dotnet run --project src/SecureMcpDesktopDemo

# (Inside the app)
# 1. Click "Start Secure MCP Server"
# 2. Click scenario buttons or use manual tools
# 3. Try Standard vs Privileged tokens
# 4. Use "Decode JWT" to inspect claims
```

You can also still run the original web + server flows.

## Screenshots / Visuals (recommended for the actual PR)

Please attach:
- Desktop app main window showing a successful "Approve" run with green verdict and full trace
- A "Needs Approval Scope" run (standard token) highlighting the red 403 on `approve_release`
- Token acquisition panel + decoded claims
- Protocol + server log tabs

## Checklist

- [x] Builds and existing tests pass
- [x] New shared library has no unnecessary dependencies
- [x] Desktop demo is self-documenting and matches original demo expectations
- [x] Architecture document updated
- [x] No breaking changes to server or existing clients (console + web continue to work)
- [x] Demo remains fully offline / deterministic by default

This PR significantly improves the discoverability and educational value of the secure MCP patterns while cleaning up the codebase for future extension.

---

**Branch:** `feature/extract-shared-models`  
**Base:** `main`