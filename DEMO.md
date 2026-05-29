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

## Talking Points

- The LLM parses and explains the release request, but the MCP server controls authorization.
- The same MCP tool contract powers the console client, the agent API, and the UI.
- Standard and privileged tokens make the authorization boundary visible.
- The raw response panel is useful for audits and demos.
