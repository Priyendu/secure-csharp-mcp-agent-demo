# Security Policy

This is a **demonstration project only**. Do not use in production.

- All JWT secrets are hardcoded for demo.
- `/auth/token` is a local demo issuer, not an OAuth/OIDC replacement.
- The privileged `mcp:tools:release` scope requires the demo-only `demo-release-secret`.
- The web agent can call OpenAI when `OPENAI_API_KEY` is configured, but MCP authorization remains server-side.
- Use proper certificate management and secret stores in real deployments.
- The auth implementation demonstrates POCs only.

Report issues via GitHub.
