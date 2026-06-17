# Demo Script

## Setup

Terminal 1:

```bash
cd src/SecureMcpServer
dotnet run --launch-profile https
```

Terminal 2:

```bash
cd src/SecureMcpAgentWeb
dotnet run --urls http://localhost:5080
```

Open `http://localhost:5080`.

Optional LLM mode:

```bash
set OPENAI_API_KEY=your-key
set OPENAI_MODEL=gpt-4o-mini
```

Without `OPENAI_API_KEY`, the app uses deterministic parsing and summaries so the demo still works offline.

## Flow

1. Select `Ready` with `Standard token`, then run the review.
   - Expected verdict: `Needs Approval Scope`.
   - The tool trace shows release status, dependencies, vulnerabilities, and a denied approval call.

2. Select `Approve` with `Privileged demo token`, then run the review.
   - Expected verdict: `Approved`.
   - The tool trace shows the approval tool returning success.

3. Select `Blocked`, then run the review.
   - Expected verdict: `Blocked`.
   - The vulnerability tool returns `CVE-DEMO-2026-0001`.

---

## Native C# WPF Desktop Demo (Recommended)

The project now includes `SecureMcpDesktopDemo` — a rich WPF application that gives you a **single-window, fully interactive, native C# experience** of the entire secure MCP system.

### How to run the desktop demo

```powershell
# From repo root
dotnet build
dotnet run --project src\SecureMcpDesktopDemo
```

Or open the solution in Visual Studio / Rider and run the `SecureMcpDesktopDemo` project.

### Inside the app (all concepts from the UI)

- **Server controls** at the top: Start / Stop the real `SecureMcpServer` as a child process. Server stdout (auth events, tool calls) appear in the Server Log tab in real time.
- **Three big scenario buttons** on the left exactly matching the original demo:
  - Ready (AnalyzerService 2.4.1 + Standard token) → 403 on approve
  - Approve (same component + Privileged token + secret) → success
  - Blocked (PaymentGateway with CVE)
- Free-form query box + "Standard / Privileged" checkbox + "Run Full Automated Review"
- "Run Step-by-Step" mode so you can watch each tool call individually
- **Token Acquisition** buttons — shows exactly what scopes each token carries
- "Decode JWT" button — displays the claims the server will validate
- **Manual Tool Call** panel — pick any of the 4 tools, any component/version, call it with the current token (perfect for showing 403 live)
- Center area: Parsed intent, big colored verdict banner, live updating tool trace list
- Right side tabs: Protocol Log (every HTTP + JSON-RPC the client sent), Server Log, Raw Last Response

The entire authorization story is visible without leaving the C# application:
- The local parser only extracts component + version.
- The MCP server (running in the child process) is the one that returns data from `demo-data.json` and enforces scopes on every `/mcp/messages` call.

This is currently the most complete way to experience the demo.

## Talking Points

- The LLM parses and explains the release request, but the MCP server controls authorization.
- The same MCP tool contract powers the console client, the agent API, and the UI.
- Standard and privileged tokens make the authorization boundary visible.
- The raw response panel is useful for audits and demos.
