# Secure C# MCP Agent Demo - Architecture Document

**Version**: 1.0  
**Audience**: Security Engineers & .NET Architects  
**Date**: May 2026  
**Status**: Demo / Educational

---

## 1. Executive Summary

This document describes a reference architecture for a **secure Model Context Protocol (MCP)** server and rule-based client agent built with .NET 8.

The system demonstrates:
- HTTPS + JWT authentication with scope-based authorization (`mcp:tools`, `mcp:tools:release`)
- Audit logging of all auth and tool events
- Tool discovery endpoint (`/mcp/tools`)
- Negative security testing (401/403 handling)
- Four MCP security analysis tools for release workflows

**Key Components**:
- `SecureMcpServer` – ASP.NET Core 8 + JWT + SSE MCP endpoints
- `SecureMcpClientAgent` – Rule-based planner + sequential tool caller
- `DemoTokenService` + `SecurityAuditLogger` – Supporting security infrastructure

---

## 2. Context Diagram (C4 Level 1)

**External Users & Systems**:

```
┌─────────────────────────────────────────────────────────────────┐
│                     External Context                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   [Security Engineer]     [DevOps / Release Manager]            │
│          │                           │                          │
│          │ JWT token request         │                          │
│          ▼                           │                          │
│   ┌──────────────────────────────┐   │                          │
│   │   SecureMcpServer            │◄──┘                          │
│   │   (HTTPS :5001)              │                              │
│   │                              │                              │
│   │  • /auth/token               │                              │
│   │  • /mcp/sse                  │                              │
│   │  • /mcp/messages             │                              │
│   │  • /mcp/tools (discovery)    │                              │
│   └──────────────────────────────┘                              │
│                 │                                               │
│                 │ JWT + mcp:tools scope                         │
│                 ▼                                               │
│   ┌──────────────────────────────┐                              │
│   │  SecureMcpClientAgent        │                              │
│   │  (Console / Rule Planner)    │                              │
│   └──────────────────────────────┘                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**External Dependencies**:
- None (self-contained demo with hardcoded demo secrets)

---

## 3. Container View (C4 Level 2)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Secure MCP Agent Demo System                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌──────────────────────────────┐        ┌──────────────────────────────┐  │
│   │   SecureMcpServer            │        │   SecureMcpClientAgent       │  │
│   │   (ASP.NET Core Web API)     │◄──────►│   (Console Application)      │  │
│   │                              │  HTTPS │                              │  │
│   │  • JWT Bearer Auth           │  +SSE  │  • Simple rule-based planner │  │
│   │  • Scope policies            │        │  • Token acquisition         │  │
│   │  • 4 security tools          │        │  • Sequential tool execution │  │
│   │  • Audit logger              │        │                              │  │
│   │  • Tool discovery            │        │                              │  │
│   └──────────────────────────────┘        └──────────────────────────────┘  │
│                 │                                                           │
│                 │ In-process DI                                             │
│                 ▼                                                           │
│   ┌──────────────────────────────────────────────────────────────────────┐  │
│   │                        Shared Services Layer                         │  │
│   │  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │  │
│   │  │ DemoTokenService │  │ ReleaseDataService│  │SecurityAuditLogger│     │  │
│   │  │ • GenerateToken  │  │ • GetReleaseStatus│  │ • LogAuthSuccess │     │  │
│   │  │ • ValidateToken  │  │ • GetDependencies │  │ • LogAuthFailure │     │  │
│   │  │                  │  │ • CheckVulns      │  │ • LogToolCall    │     │  │
│   │  │                  │  │ • ApproveRelease  │  │                  │     │  │
│   │  └──────────────────┘  └──────────────────┘  └──────────────────┘     │  │
│   └──────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│   Data: demo/*.json (embedded)                                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Technology Stack**:
- .NET 8, ASP.NET Core Minimal APIs
- JWT Bearer (symmetric key for demo)
- Server-Sent Events (SSE) for MCP transport
- JSON-RPC 2.0 over `/mcp/messages`

---

## 4. Security & Auth Flow Diagram

```
┌──────────────┐     1. POST /auth/token          ┌──────────────────────┐
│   Client     │─────────────────────────────────►│  SecureMcpServer     │
│   (Agent)    │    {ClientId, Scopes}            │                      │
└──────────────┘                                  │  DemoTokenService    │
      │                                           │  GenerateToken()     │
      │ 2. 200 OK {access_token}                  └──────────────────────┘
      │
      ▼
┌──────────────┐     3. GET /mcp/sse              ┌──────────────────────┐
│              │     Authorization: Bearer ...    │                      │
│              │─────────────────────────────────►│  /mcp/sse            │
│              │                                  │  • Validate token    │
│              │                                  │  • Check mcp:tools   │
│              │◄─────────────────────────────────│  • Return SSE stream │
│              │    4. endpoint event             │                      │
└──────────────┘                                  └──────────────────────┘

      │
      │ 5. POST /mcp/messages
      │    jsonrpc + tools/call
      │    (or tools/list)
      ▼
┌──────────────┐                                  ┌──────────────────────┐
│              │─────────────────────────────────►│  /mcp/messages       │
│              │    Bearer token                  │  • Validate JWT      │
│              │                                  │  • Require scope     │
│              │◄─────────────────────────────────│  • Audit + dispatch  │
│              │    JSON-RPC result               │                      │
└──────────────┘                                  └──────────────────────┘

Negative Paths:
- Missing / invalid token → 401
- Valid token, missing scope → 403
- approve_release without mcp:tools:release → 403
```

**Authorization Policies**:
- `McpAccess`: Requires claim `scope = mcp:tools`
- `ReleaseAccess`: Requires claim `scope = mcp:tools:release`

---

## 5. Component Interaction Summary

| Component              | Responsibility                              | Security Controls                     |
|------------------------|---------------------------------------------|---------------------------------------|
| DemoTokenService       | Issue & validate JWT tokens                 | Symmetric signing, claims injection   |
| SecurityAuditLogger    | Centralized logging of auth + tool calls    | Structured logs for SIEM integration  |
| ReleaseDataService     | Business data + security tooling            | Scope check on approve_release        |
| /mcp/sse + /messages   | MCP protocol transport                      | JWT + scope middleware                |
| /mcp/tools             | Declarative tool discovery                  | Auth required                         |
| Client Planner         | Simple rule-based planning                  | No internal auth (delegates to server)|

---

## 6. Files & Project Structure

```
secure-csharp-mcp-agent-demo/
├── src/
│   ├── SecureMcpServer/
│   │   ├── Program.cs              # All endpoints + auth
│   │   ├── Security/
│   │   │   ├── DemoTokenService.cs
│   │   │   ├── SecurityAuditLogger.cs
│   │   │   └── RequireScopeAttribute.cs
│   │   └── Tools/
│   │       └── ReleaseDataService.cs
│   └── SecureMcpClient/
│       └── Program.cs              # Planner + sequential calls
├── tests/
├── docs/
│   └── ARCHITECTURE.md             # This document
└── README.md
```

---

## 7. Recommendations for Production Hardening

1. Replace symmetric JWT key with RSA or ECDSA + proper key rotation.
2. Move token issuance behind OAuth2 / OIDC provider (IdentityServer, Auth0, Entra ID).
3. Store audit logs in a tamper-proof sink (Event Hubs + immutable storage).
4. Add rate limiting and request validation middleware.
5. Implement proper certificate management for HTTPS.
6. Add integration tests for all negative auth cases.

---

*This architecture document is intended for educational and demonstration purposes only.*
