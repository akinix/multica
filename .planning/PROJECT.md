# Multica Go-to-C# Migration

## What This Is

A full backend language migration for Multica, an AI-native task management platform. The Go backend (~97K lines of non-test code) is being replaced with C# using ASP.NET Core Minimal API and EF Core. The frontend (TypeScript/React) remains unchanged — this is a drop-in backend replacement that preserves 100% API compatibility.

## Core Value

100% API-compatible C# backend that serves as a transparent replacement for the Go server — existing web, desktop, and mobile clients work without any modifications.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

(None yet — migration in progress)

### Active

<!-- Current scope. Building toward these. -->

- [ ] ASP.NET Core Minimal API server with EF Core data access
- [ ] Full authentication system (JWT, PAT, daemon tokens, Google OAuth, CloudFront signing)
- [ ] All HTTP endpoints matching Go backend routes and response shapes
- [ ] WebSocket realtime system with scoped broadcasting
- [ ] Task lifecycle service (claim, start, complete, fail, progress)
- [ ] Daemon process for local agent execution
- [ ] CLI tool (.NET global tool) with all 18 command groups
- [ ] Docker/Helm deployment configuration
- [ ] CI/CD pipeline for .NET

### Out of Scope

<!-- Explicit boundaries. Includes reasoning to prevent re-adding. -->

- Frontend modifications — frontend stays TypeScript/React, only backend changes
- Database schema changes — migration is code-only, schema stays identical
- New features — focus is 1:1 port, not feature additions
- Performance optimization — v1 goal is correctness; performance tuning is v2
- Mobile app changes — mobile is independent, shares only types from core

## Context

**Migration inventory (2026-05-28):**
- 96,611 lines of non-test Go code across ~120 files
- 74,901 lines of test code across ~80 files
- 34 SQL query files (3,625 lines) to convert to EF Core
- 139 database migrations (5,033 lines)
- 50+ HTTP handler files (30,393 lines)
- 12 daemon files (12,383 lines)
- 18 CLI command groups (10,273 lines)

**Key complexity areas:**
- `handler/issue.go` (3,098 lines) — largest handler
- `daemon/daemon.go` (3,417 lines) — largest single file
- `service/task.go` (2,277 lines) — critical task lifecycle
- Auth middleware with 4 token types
- Realtime system (WebSocket + Redis Streams)

**Go dependencies requiring C# equivalents:**
- go-chi/chi → ASP.NET Core routing
- jackc/pgx → Npgsql (via EF Core)
- gorilla/websocket → ASP.NET Core WebSockets
- redis/go-redis → StackExchange.Redis
- aws-sdk-go-v2 → AWSSDK.S3/SecretsManager
- spf13/cobra → System.CommandLine
- golang-jwt → System.IdentityModel.Tokens.Jwt
- prometheus/client_golang → prometheus-net

## Constraints

- **API Compatibility**: All HTTP endpoints must return identical JSON shapes — desktop app versions in the field depend on this
- **Database**: Must work with existing PostgreSQL 17 + pgvector database, no schema changes
- **No frontend changes**: TypeScript/React frontend is not part of this migration
- **Token prefixes**: Must preserve `mul_`, `mdt_`, `mat_`, `mcn_` token prefix conventions
- **Cookie names**: Must preserve `multica_auth` cookie name and attributes
- **Header names**: Must preserve X-Workspace-Slug, X-User-ID, X-Client-* headers

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| ASP.NET Core Minimal API | Modern, lightweight, performance-competitive with Go Chi | — Pending |
| EF Core for data access | Mature ORM with PostgreSQL support, migration tooling | — Pending |
| Full replacement (not strangler fig) | Open source project, no production users to protect | — Pending |
| System.CommandLine for CLI | Official .NET CLI framework, extensible | — Pending |
| Serilog for logging | Structured logging with JSON output, widely used in .NET | — Pending |
| SignalR or raw WebSockets for realtime | TBD — depends on scoped subscription model fit | — Pending |

## Evolution

<!-- Updated after each phase/milestone -->

---
*Created: 2026-05-28*
*Last updated: 2026-05-28 after initial project setup*
