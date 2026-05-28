---
phase: 2
plan: 01
subsystem: auth
tags: [auth, jwt, token, redis, caching]
requires: [AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05]
provides: [jwt-token-service, pat-cache, daemon-token-cache, task-token-validator, cloud-pat-verifier]
tech-stack:
  added: [System.IdentityModel.Tokens.Jwt, StackExchange.Redis]
  patterns: [redis-caching, fail-open]
key-files:
  created:
    - server/src/Multica.Core/Auth/TokenHasher.cs
    - server/src/Multica.Core/Auth/JwtTokenService.cs
    - server/src/Multica.Core/Auth/PatCache.cs
    - server/src/Multica.Core/Auth/DaemonTokenCache.cs
    - server/src/Multica.Core/Auth/TaskTokenIdentity.cs
    - server/src/Multica.Core/Auth/CloudPatIdentity.cs
    - server/src/Multica.Core/Auth/CloudPatVerifier.cs
    - server/src/Multica.Infrastructure/Auth/TaskTokenValidator.cs
  modified:
    - server/src/Multica.Core/Multica.Core.csproj
    - server/src/Multica.Infrastructure/DependencyInjection.cs
    - server/src/Multica.Infrastructure/Multica.Infrastructure.csproj
    - server/src/Multica.Api/appsettings.json
key-decisions:
  - TokenHasher uses SHA256.HashData for Go-compatible hashing
  - JWT uses HMAC-SHA256 with configurable secret and TTL
  - Redis caches use fail-open pattern (errors logged, not thrown)
  - CloudPatVerifier returns null when Fleet URL not configured
requirements-completed: [AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05]
duration: 15 min
completed: 2026-05-28
---

# Phase 2 Plan 01: Token Generation, Validation & Caching Summary

Implemented all token types (JWT, PAT, daemon, task, cloud PAT) with Redis caching — the foundation for the auth middleware.

## What Was Built

### TokenHasher
- Static class with SHA-256 token hashing
- Compatible with Go's `auth.HashToken()`: `SHA256.HashData → Convert.ToHexString().ToLowerInvariant()`

### JwtTokenService
- JWT generation with HMAC-SHA256 signing
- Claims: `sub` (userId), `email`, `jti`
- Configurable secret, TTL (default 30 days)
- Validation with 5-minute clock skew tolerance

### PatCache (mul_)
- Redis-backed cache with key prefix `mul:auth:pat:`
- TTL clamping: `min(cacheTTL, remaining lifetime)`
- Fail-open: Redis errors logged, not thrown
- Null-safe: all methods no-op when Redis unavailable

### DaemonTokenCache (mdt_)
- Redis-backed cache with key prefix `mul:auth:daemon:`
- DaemonTokenIdentity stored as JSON with short keys (w, d) matching Go
- Same fail-open pattern as PatCache

### TaskTokenValidator (mat_)
- Database lookup by SHA-256 hash (no cache — single-use, short-lived)
- Queries TaskToken table, returns TaskTokenIdentity with UserId, AgentId, TaskId, WorkspaceId
- Returns null for invalid or expired tokens

### CloudPatVerifier (mcn_)
- Redis cache (60s TTL) with key prefix `mul:auth:mcn:`
- Fleet API: `POST /api/v1/pat/verify`
- Owner existence check via callback
- Negative results NOT cached (valid=false, owner_unknown)
- Error types: CloudPatInvalidException, CloudPatUnavailableException

### DI Registration
- JwtTokenService, PatCache, DaemonTokenCache: singleton
- TaskTokenValidator: scoped (needs DbContext)
- CloudPatVerifier: singleton with HttpClient factory

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

1. ✅ TokenHasher.HashToken("test") matches Go output: `9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08`
2. ✅ All Redis cache key prefixes match Go implementation
3. ✅ Build succeeds for all projects
4. ✅ All services registered in DI container

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.

## Next Steps

Ready for Plan 02: Cookie Auth, CSRF, Google OAuth & CloudFront
