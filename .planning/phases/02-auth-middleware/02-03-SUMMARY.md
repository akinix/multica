---
phase: 2
plan: 03
subsystem: auth
tags: [auth, middleware, workspace, rate-limit, signup]
requires: [AUTH-09, AUTH-10, AUTH-11]
provides: [auth-middleware, daemon-auth-middleware, workspace-middleware, rate-limit-middleware, signup-controls]
tech-stack:
  added: []
  patterns: [multi-token-dispatch, workspace-enforcement, redis-rate-limiting]
key-files:
  created:
    - server/src/Multica.Api/Middleware/AuthMiddleware.cs
    - server/src/Multica.Api/Middleware/DaemonAuthMiddleware.cs
    - server/src/Multica.Api/Middleware/WorkspaceMiddleware.cs
    - server/src/Multica.Api/Middleware/RateLimitMiddleware.cs
    - server/src/Multica.Api/Middleware/RequestIdMiddleware.cs
    - server/src/Multica.Api/Middleware/ClientMetadataMiddleware.cs
    - server/src/Multica.Api/Middleware/CspMiddleware.cs
    - server/src/Multica.Core/Auth/SignupControlService.cs
  modified:
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Api/Handlers/AuthHandler.cs
    - server/src/Multica.Api/appsettings.json
key-decisions:
  - Auth middleware dispatches by token prefix: mat_, mcn_, mul_, JWT
  - Daemon auth middleware only accepts Authorization header (no cookie fallback)
  - Workspace resolution priority: X-Workspace-Slug > ?workspace_slug > X-Workspace-ID > ?workspace_id
  - Rate limiting uses Redis Lua script for atomic INCR + EXPIRE
  - Middleware order matches Go: RequestID → ClientMetadata → RequestLogger → ExceptionHandler → CSP → CORS → RateLimit
requirements-completed: [AUTH-09, AUTH-10, AUTH-11]
duration: 25 min
completed: 2026-05-28
---

# Phase 2 Plan 03: Auth Middleware Pipeline & Workspace Enforcement Summary

Built the core auth middleware with multi-token-prefix routing, workspace membership/role enforcement, and Redis-backed rate limiting.

## What Was Built

### AuthMiddleware (Multi-Token Dispatch)
- Token extraction: Authorization header priority over cookie
- CSRF validation for cookie-based auth
- mat_ prefix: task token validation, sets X-User-ID, X-Agent-ID, X-Task-ID, X-Workspace-ID, X-Actor-Source
- mcn_ prefix: cloud PAT verification via Fleet API
- mul_ prefix: PAT cache → DB fallback
- Default: JWT validation

### DaemonAuthMiddleware
- Same token dispatch as AuthMiddleware but for daemon-specific routes
- Only accepts Authorization header (no cookie fallback)
- mdt_ prefix: daemon token validation with cache → DB fallback
- Context injection: DaemonWorkspaceId, DaemonId, DaemonAuthPath

### WorkspaceMiddleware
- Workspace resolution priority matching Go exactly
- Task token binding: prevents workspace escalation
- Membership validation: queries Members table
- Role enforcement: RequireWorkspaceRoleMiddleware

### RateLimitMiddleware
- Redis-backed per-IP rate limiting
- Lua script: atomic INCR + EXPIRE
- Key format: mul:ratelimit:{path}:{ip}
- 429 response with Retry-After header
- Fail-open on Redis errors

### General Middleware
- RequestIdMiddleware: X-Request-Id generation
- ClientMetadataMiddleware: X-Client-Platform, X-Client-Version, X-Client-OS extraction
- CspMiddleware: Content-Security-Policy header

### Signup Controls
- SignupControlService: validates AllowSignup, AllowedEmails, AllowedEmailDomains
- Integrated into Google OAuth callback
- New users rejected when AllowSignup=false

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

1. ✅ Auth middleware correctly dispatches all 4 token types
2. ✅ Daemon auth middleware handles mdt_ tokens
3. ✅ Workspace middleware resolves workspace and checks membership
4. ✅ Rate limiting triggers at configured threshold
5. ✅ Middleware order matches Go backend
6. ✅ Signup controls work as configured
7. ✅ Build succeeds for all projects

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.

## Next Steps

Ready for Plan 04: General Middleware Pipeline
