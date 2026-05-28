# Secure C# MCP Agent Demo

Secure MCP (Model Context Protocol) server and client demo using .NET 8, JWT authentication, scope-based authorization, and rule-based agent planning.

## Features

- ASP.NET Core MCP server over HTTPS + SSE
- JWT auth + scope policies (`mcp:tools`, `mcp:tools:release`)
- 4 MCP tools: get_release_status, get_dependencies, check_security_vulnerabilities, approve_release
- Rule-based planner in console client
- Security audit logging of all tool calls and auth events
- Demo JSON data + negative auth tests
- GitHub-ready structure

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

## Folder Structure

```
secure-csharp-mcp-agent-demo/
├── src/
│   ├── SecureMcpServer/
│   └── SecureMcpClient/
├── tests/
├── .github/workflows/
├── README.md
├── SECURITY.md
└── .gitignore
```

## Verification

- `dotnet build` succeeds
- Server runs HTTPS on 5001
- Client completes planner sequence successfully for the example query

MIT License - For educational/demo use only.