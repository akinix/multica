# Roadmap: Multica Go-to-C# Migration

**Created:** 2026-05-28
**Total Go codebase:** 96,611 lines non-test | 74,901 lines test
**Strategy:** Full replacement — no Go code retained

## Phase 1: Foundation & Data Layer ✅

**Goal:** ASP.NET Core project running with EF Core connected to existing PostgreSQL database.

**Status:** Complete (2026-05-28)
**Estimated effort:** Large
**Risk:** Medium — EF Core schema mapping must be exact

### Tasks

1. ✅ Create `server/` directory with ASP.NET Core Minimal API project structure
2. ✅ Set up solution file with projects: `Multica.Api`, `Multica.Core`, `Multica.Infrastructure`
3. ✅ Configure EF Core with Npgsql provider
4. ✅ Map all 49 entity types manually (Docker unavailable for scaffold)
5. ✅ Configure connection pooling (NpgsqlDataSourceBuilder)
6. ✅ Set up StackExchange.Redis connection
7. ✅ Implement health check endpoints (/health, /readyz, /healthz)
8. ✅ Configure Serilog structured logging (CompactJsonFormatter)
9. ✅ Set up basic middleware pipeline (CORS, ExceptionHandler, SerilogRequestLogging)
10. ✅ Create 12 entity configurations (User, Workspace, Member, Issue, Agent, Comment, AgentTaskQueue, AgentSkill, IssueToLabel, TaskUsageHourly, TaskUsageHourlyDirty, GithubPullRequestCheckSuite)
11. ✅ Write integration tests (3 passing: DbSet count, root endpoint)

**Requirements covered:** FND-01..06, FEAT-20

**Deliverables:**

- `server/Multica.slnx` — Solution with 4 projects
- `server/src/Multica.Api/` — Minimal API host (port 8080)
- `server/src/Multica.Core/Entities/` — 49 entity classes
- `server/src/Multica.Core/Enums/` — 8 enum types
- `server/src/Multica.Infrastructure/` — DbContext, Redis, DI, health checks
- `server/src/Multica.Infrastructure/Data/Configurations/` — 12 entity configurations
- `server/tests/Multica.Infrastructure.Tests/` — Integration tests

---

## Phase 2: Auth & Middleware ✅

**Goal:** Full authentication and authorization middleware chain matching Go backend.

**Status:** Complete (2026-05-28)
**Estimated effort:** Large
**Risk:** High — 4 token types, CSRF, CloudFront signing

### Tasks

1. Implement JWT token generation and validation (HMAC-SHA256)
2. Implement PAT (`mul_`) token validation with SHA-256 hash lookup
3. Implement daemon token (`mdt_`) validation
4. Implement task token (`mat_`) validation bound to (agent_id, task_id)
5. Implement Cloud PAT (`mcn_`) validation against Multica Cloud Fleet
6. Implement cookie auth (HttpOnly + HMAC-bound CSRF)
7. Implement Google OAuth 2.0 flow
8. Implement CloudFront signed cookie/URL generation
9. Build auth middleware with multi-token-prefix routing
10. Build workspace membership/role middleware
11. Build per-IP rate limiting middleware (Redis-backed)
12. Build remaining middleware: ClientMetadata, RequestLogger, CSP, Metrics
13. Implement signup controls (ALLOW_SIGNUP, ALLOWED_EMAILS, ALLOWED_EMAIL_DOMAINS)

**Requirements covered:** AUTH-01..12, MW-01..07

**Verification:**
- JWT login flow works end-to-end
- PAT auth works for programmatic access
- Workspace role enforcement blocks unauthorized access
- Rate limiting triggers on excessive requests

---

## Phase 3: Issues & Comments ✅

**Goal:** Issue and comment domain fully functional — the core of the product.

**Status:** Complete (2026-05-29)
**Estimated effort:** Very Large
**Risk:** High — issue.go is 3,098 lines, comment.go is 1,269 lines

### Tasks

1. Implement Issue CRUD endpoints
2. Implement issue search with full-text and filters
3. Implement batch issue operations
4. Implement parent-child relationships with progress tracking
5. Implement issue labels
6. Implement issue metadata
7. Implement issue reactions
8. Implement issue attachments
9. Implement issue PR tracking
10. Implement issue task management
11. Implement grouped issue listing
12. Implement issue subscriptions
13. Implement Comment CRUD endpoints
14. Implement comment resolve/unresolve
15. Implement comment reactions
16. Implement comment timeline view

**Plans:** 10 plans in 4 waves

Plans:
- [x] 03-01-PLAN.md — Issue CRUD endpoints
- [x] 03-02-PLAN.md — Comment CRUD endpoints
- [x] 03-03-PLAN.md — Issue search, filters & grouped listing
- [x] 03-04-PLAN.md — Batch issue operations
- [x] 03-05-PLAN.md — Parent-child relationships & progress tracking
- [x] 03-06-PLAN.md — Issue labels & metadata
- [x] 03-07-PLAN.md — Issue & comment reactions
- [x] 03-08-PLAN.md — Issue attachments & PR tracking
- [x] 03-09-PLAN.md — Issue task management & subscriptions
- [x] 03-10-PLAN.md — Comment resolve/unresolve & timeline

**Verification:**
- Full issue lifecycle works (create → update → search → delete)
- Comment CRUD with reactions works
- Parent-child progress tracking matches Go behavior
- Batch operations work correctly
---

## Phase 4: Agents, Skills & Runtimes

**Goal:** Agent management, skill system, and runtime management fully functional.

**Estimated effort:** Large
**Risk:** Medium — agent.go (1,264 lines), skill.go (1,847 lines)

### Tasks

1. Implement Agent CRUD endpoints
2. Implement agent archive/restore
3. Implement agent skills management
4. Implement agent environment variables
5. Implement agent task listing
6. Implement agent template catalog
7. Implement Skill CRUD endpoints
8. Implement skill file management
9. Implement skill import
10. Implement Runtime CRUD endpoints
11. Implement runtime usage tracking
12. Implement runtime update request/response flow
13. Implement runtime model listing
14. Implement runtime local skills
15. Implement runtime liveness tracking
16. Implement runtime archive and delete

**Requirements covered:** AGT-01..06, SKL-01..03, RT-01..07 (runtimes)

**Verification:**
- Agent CRUD with skills and env vars works
- Skill file management works
- Runtime lifecycle works end-to-end

---

## Phase 5: Workspaces, Projects & Squads

**Goal:** Workspace management, project system, and squad management fully functional.

**Estimated effort:** Medium
**Risk:** Medium — workspace auth is cross-cutting

### Tasks

1. Implement Workspace CRUD endpoints
2. Implement workspace member management
3. Implement workspace invitation flow
4. Implement workspace revocation
5. Implement Project CRUD endpoints
6. Implement project resources management
7. Implement Squad CRUD endpoints
8. Implement squad member management
9. Implement squad briefing

**Requirements covered:** WS-01..04, PRJ-01..02, SQD-01..03

**Verification:**
- Workspace creation and member management works
- Invitation flow (create → accept/decline) works
- Project and squad CRUD works

---

## Phase 6: Realtime System

**Goal:** WebSocket hub with scoped broadcasting and Redis relay matching Go backend.

**Estimated effort:** Large
**Risk:** High — SignalR + Redis backplane needs careful design

### Tasks

1. Implement WebSocket hub with connection management
2. Implement room subscription model (workspace, user, task, chat, daemon_runtime scopes)
3. Implement scoped broadcasting
4. Implement Redis Streams relay for multi-node delivery
5. Implement sharded Redis streams
6. Implement realtime metrics
7. Verify event types match existing TypeScript definitions

**Requirements covered:** RT-01..06 (realtime)

**Verification:**
- WebSocket connections work
- Scoped broadcasting delivers to correct rooms
- Redis relay works across multiple server instances
- Event types match frontend expectations

---

## Phase 7: Services & Events

**Goal:** Business logic services and event bus fully functional.

**Estimated effort:** Large
**Risk:** High — task.go is 2,277 lines of critical business logic

### Tasks

1. Implement TaskService — full task lifecycle
2. Implement TaskService — usage tracking and heartbeat
3. Implement TaskService — orphan recovery
4. Implement AutopilotService — execution and trigger evaluation
5. Implement email service (Resend + SMTP fallback)
6. Implement empty claim cache (Redis optimization)
7. Implement event bus (in-process pub/sub)
8. Implement event listeners (notifications, activity, subscriptions, autopilot triggers)

**Requirements covered:** SVC-01..08

**Verification:**
- Task lifecycle (claim → start → complete) works
- Autopilot triggers fire correctly
- Email sending works via Resend
- Event bus decouples handlers from side effects

---

## Phase 8: Remaining Features

**Goal:** All remaining HTTP endpoints functional.

**Estimated effort:** Medium
**Risk:** Low — these are mostly standard CRUD

### Tasks

1. Implement Autopilot CRUD, triggers, runs, deliveries
2. Implement autopilot webhook ingress
3. Implement GitHub App integration (webhooks, installations)
4. Implement chat sessions, messages, pending tasks
5. Implement inbox management
6. Implement onboarding flows
7. Implement dashboard usage reports
8. Implement label CRUD
9. Implement pin management
10. Implement attachment management
11. Implement file upload handling
12. Implement activity logging
13. Implement notification preferences
14. Implement PAT CRUD
15. Implement feedback and contact sales
16. Implement user profile
17. Implement config endpoint
18. Implement local file serving

**Requirements covered:** FEAT-01..21

**Verification:**
- All remaining endpoints return correct responses
- GitHub webhook handling works
- File upload/download works

---

## Phase 9: Daemon

**Goal:** Daemon process fully functional in C#.

**Estimated effort:** Very Large
**Risk:** High — daemon.go is 3,417 lines, complex process management

### Tasks

1. Implement daemon registration, deregistration, heartbeat
2. Implement task claim/start/complete/fail lifecycle
3. Implement task progress reporting and usage tracking
4. Implement GC checks
5. Implement orphan recovery and session pinning
6. Implement daemon WebSocket hub
7. Implement daemon core loop (task execution, process management)
8. Implement daemon configuration
9. Implement agent prompt construction
10. Implement garbage collection
11. Implement local directory management
12. Implement daemon identity management
13. Implement local skill detection
14. Implement daemon auto-update
15. Implement execution environment setup
16. Implement repository cache management

**Requirements covered:** DMN-01..16

**Verification:**
- Daemon registers and heartbeats
- Task execution lifecycle works
- Process management works
- Auto-update mechanism works

---

## Phase 10: CLI

**Goal:** .NET global tool CLI matching Go CLI functionality.

**Estimated effort:** Very Large
**Risk:** Medium — 18 command groups, issue.go alone is 60.5K

### Tasks

1. Set up CLI project with System.CommandLine
2. Implement `issue` command group
3. Implement `agent` command group
4. Implement `project` command group
5. Implement `workspace` command group
6. Implement `daemon` command group
7. Implement `runtime` command group
8. Implement `skill` command group
9. Implement `squad` command group
10. Implement `autopilot` command group
11. Implement `auth` command group
12. Implement `label` command group
13. Implement `config` command group
14. Implement CLI self-update mechanism
15. Implement CLI configuration management
16. Package as .NET global tool

**Requirements covered:** CLI-01..15

**Verification:**
- All CLI commands produce correct output
- CLI can authenticate and interact with API
- Self-update works

---

## Phase 11: Infrastructure & Testing

**Goal:** Production-ready container, CI pipeline, and integration tests.

**Estimated effort:** Medium
**Risk:** Low

### Tasks

1. Create Dockerfile for C# backend
2. Update Docker Compose self-host configuration
3. Update Helm chart for C# backend
4. Update GitHub Actions CI for .NET
5. Verify Prometheus metrics compatibility
6. Run full API compatibility test suite
7. Performance baseline measurement
8. Documentation updates

**Requirements covered:** INFRA-01..06, API-01..05

**Verification:**
- Docker container builds and runs
- CI pipeline passes
- All existing E2E tests pass against C# backend
- Prometheus metrics are scrapable

---

## Phase Dependency Graph

```
Phase 1 (Foundation)
  └─→ Phase 2 (Auth & Middleware)
        └─→ Phase 3 (Issues & Comments)
        └─→ Phase 4 (Agents, Skills, Runtimes)
        └─→ Phase 5 (Workspaces, Projects, Squads)
        └─→ Phase 6 (Realtime)
        └─→ Phase 7 (Services & Events)
              └─→ Phase 8 (Remaining Features)
              └─→ Phase 9 (Daemon)
              └─→ Phase 10 (CLI)
                    └─→ Phase 11 (Infrastructure & Testing)
```

Phases 3-7 can be parallelized after Phase 2 completes.
Phase 8 can start after Phase 7.
Phases 9-10 depend on Phase 7.
Phase 11 is the final integration.

---
*Roadmap created: 2026-05-28*
