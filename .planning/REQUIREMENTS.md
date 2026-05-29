# Requirements: Multica Go-to-C# Migration

**Defined:** 2026-05-28
**Core Value:** 100% API-compatible C# backend that drops in as a replacement for the Go server, preserving all existing frontend functionality without modification.

## v1 Requirements

Requirements for the migration. Each maps to roadmap phases.

### Foundation & Data Layer

- [ ] **FND-01**: ASP.NET Core Minimal API project compiles and runs with health check endpoint
- [ ] **FND-02**: EF Core DbContext maps all existing PostgreSQL tables (139 migrations worth of schema)
- [ ] **FND-03**: Database connection pooling works with pgx-compatible performance
- [ ] **FND-04**: EF Core DbContext exposes DbSets for all 34 query domains (actual LINQ/raw SQL query implementation deferred to Phase 3+ domain phases)
- [ ] **FND-05**: Existing database migrations are preserved and applied via EF Core migration bundle
- [ ] **FND-06**: Redis connection via StackExchange.Redis for caching and rate limiting

### Authentication & Authorization

- [ ] **AUTH-01**: JWT (HMAC-SHA256) token generation and validation for browser sessions
- [ ] **AUTH-02**: PAT (`mul_` prefix) token validation with SHA-256 hash lookup
- [ ] **AUTH-03**: Daemon token (`mdt_` prefix) validation for daemon-to-server auth
- [ ] **AUTH-04**: Task token (`mat_` prefix) validation bound to (agent_id, task_id)
- [ ] **AUTH-05**: Cloud PAT (`mcn_` prefix) validation against Multica Cloud Fleet API
- [ ] **AUTH-06**: Cookie auth with HttpOnly `multica_auth` cookie + HMAC-bound CSRF token
- [ ] **AUTH-07**: Google OAuth 2.0 login/signup flow with code exchange
- [ ] **AUTH-08**: CloudFront signed cookie/URL generation (RSA-SHA1, AWS Secrets Manager)
- [ ] **AUTH-09**: Multi-token-prefix routing in auth middleware (4 token types)
- [ ] **AUTH-10**: Workspace membership/role enforcement middleware (member/admin/owner)
- [ ] **AUTH-11**: Per-IP rate limiting via Redis (auth endpoints, webhook endpoints)
- [ ] **AUTH-12**: Signup controls (ALLOW_SIGNUP, ALLOWED_EMAILS, ALLOWED_EMAIL_DOMAINS)

### Middleware

- [ ] **MW-01**: Request ID generation middleware
- [ ] **MW-02**: Client metadata extraction (X-Client-Platform, X-Client-Version, X-Client-OS)
- [ ] **MW-03**: Structured request logging with slow-request detection
- [ ] **MW-04**: Prometheus HTTP metrics middleware
- [ ] **MW-05**: Content-Security-Policy header middleware
- [ ] **MW-06**: CORS middleware with configurable origins
- [ ] **MW-07**: Panic/exception recovery middleware

### Core Domain: Issues

- [ ] **ISSUE-01**: Issue CRUD (create, read, update, delete)
- [ ] **ISSUE-02**: Issue search with full-text and filter support
- [ ] **ISSUE-03**: Batch issue operations (update, delete)
- [ ] **ISSUE-04**: Parent-child issue relationships with progress tracking
- [ ] **ISSUE-05**: Issue labels (add, remove, list)
- [ ] **ISSUE-06**: Issue metadata management
- [ ] **ISSUE-07**: Issue reactions (add, remove, list)
- [ ] **ISSUE-08**: Issue attachments
- [ ] **ISSUE-09**: Issue PR tracking (GitHub integration)
- [ ] **ISSUE-10**: Issue task management (claim, start, complete, cancel)
- [ ] **ISSUE-11**: Grouped issue listing with configurable grouping
- [ ] **ISSUE-12**: Issue subscriptions

### Core Domain: Comments

- [ ] **CMT-01**: Comment CRUD (create, read, update, delete)
- [x] **CMT-02**: Comment resolve/unresolve
- [ ] **CMT-03**: Comment reactions
- [x] **CMT-04**: Comment timeline view

### Core Domain: Agents

- [ ] **AGT-01**: Agent CRUD (create, read, update, delete)
- [ ] **AGT-02**: Agent archive/restore
- [ ] **AGT-03**: Agent skills management
- [ ] **AGT-04**: Agent environment variables management
- [ ] **AGT-05**: Agent task listing
- [ ] **AGT-06**: Agent template catalog

### Core Domain: Skills

- [ ] **SKL-01**: Skill CRUD (create, read, update, delete)
- [ ] **SKL-02**: Skill file management
- [ ] **SKL-03**: Skill import

### Core Domain: Workspaces

- [ ] **WS-01**: Workspace CRUD (create, read, update, delete)
- [ ] **WS-02**: Workspace member management (add, remove, role update)
- [ ] **WS-03**: Workspace invitation flow (create, accept, decline)
- [ ] **WS-04**: Workspace revocation

### Core Domain: Projects

- [ ] **PRJ-01**: Project CRUD (create, read, update, delete)
- [ ] **PRJ-02**: Project resources management

### Core Domain: Squads

- [ ] **SQD-01**: Squad CRUD (create, read, update, delete)
- [ ] **SQD-02**: Squad member management
- [ ] **SQD-03**: Squad briefing

### Core Domain: Runtimes

- [ ] **RT-01**: Runtime CRUD (create, read, update, delete)
- [ ] **RT-02**: Runtime usage tracking and aggregation
- [ ] **RT-03**: Runtime update request/response flow
- [ ] **RT-04**: Runtime model listing
- [ ] **RT-05**: Runtime local skills (list, import)
- [ ] **RT-06**: Runtime liveness tracking
- [ ] **RT-07**: Runtime archive and delete

### Realtime System

- [ ] **RT-01**: WebSocket hub with connection management and room subscriptions
- [ ] **RT-02**: Scoped broadcasting (workspace, user, task, chat, daemon_runtime)
- [ ] **RT-03**: Redis Streams relay for multi-node event delivery
- [ ] **RT-04**: Sharded Redis streams for horizontal scaling
- [ ] **RT-05**: Realtime metrics (connections, evictions, send QPS)
- [ ] **RT-06**: Client-side event types matching existing TypeScript definitions

### Service Layer

- [ ] **SVC-01**: TaskService — task lifecycle (claim, start, complete, fail, cancel, progress)
- [ ] **SVC-02**: TaskService — usage tracking and heartbeat management
- [ ] **SVC-03**: TaskService — orphan recovery
- [ ] **SVC-04**: AutopilotService — autopilot execution and trigger evaluation
- [ ] **SVC-05**: Email service (Resend API + SMTP fallback)
- [ ] **SVC-06**: Empty claim cache (Redis-backed optimization)
- [ ] **SVC-07**: Event bus (in-process pub/sub for decoupling handlers from side effects)
- [ ] **SVC-08**: Event listeners (notifications, activity logging, auto-subscription, autopilot triggers)

### Remaining Features

- [ ] **FEAT-01**: Autopilot CRUD and trigger management
- [ ] **FEAT-02**: Autopilot webhook ingress
- [ ] **FEAT-03**: Autopilot runs and deliveries
- [ ] **FEAT-04**: GitHub App integration (webhooks, installation management)
- [ ] **FEAT-05**: Chat sessions, messages, and pending tasks
- [ ] **FEAT-06**: Inbox management (list, read, archive, unread count)
- [ ] **FEAT-07**: Onboarding flows
- [ ] **FEAT-08**: Dashboard usage reports (daily, by-agent, runtime)
- [ ] **FEAT-09**: Label CRUD
- [ ] **FEAT-10**: Pin management (CRUD, reorder)
- [ ] **FEAT-11**: Attachment management
- [ ] **FEAT-12**: File upload handling
- [ ] **FEAT-13**: Activity logging
- [ ] **FEAT-14**: Notification preferences
- [ ] **FEAT-15**: Personal access token CRUD
- [ ] **FEAT-16**: Feedback submission
- [ ] **FEAT-17**: Contact sales form
- [ ] **FEAT-18**: User profile and onboarding
- [ ] **FEAT-19**: Config endpoint
- [ ] **FEAT-20**: Health/readiness endpoints (/health, /readyz, /healthz)
- [ ] **FEAT-21**: Local file serving (/uploads/*)

### Daemon

- [ ] **DMN-01**: Daemon registration, deregistration, heartbeat
- [ ] **DMN-02**: Task claim/start/complete/fail lifecycle
- [ ] **DMN-03**: Task progress reporting and usage tracking
- [ ] **DMN-04**: GC checks (issues, chat sessions, autopilot runs, tasks)
- [ ] **DMN-05**: Orphan recovery and session pinning
- [ ] **DMN-06**: Daemon WebSocket hub
- [ ] **DMN-07**: Daemon core loop (task execution, process management)
- [ ] **DMN-08**: Daemon configuration and workspace config resolution
- [ ] **DMN-09**: Agent prompt construction
- [ ] **DMN-10**: Garbage collection of old work directories
- [ ] **DMN-11**: Local directory/workspace management
- [ ] **DMN-12**: Daemon identity management
- [ ] **DMN-13**: Local skill detection and reporting
- [ ] **DMN-14**: Daemon auto-update
- [ ] **DMN-15**: Execution environment setup (execenv)
- [ ] **DMN-16**: Repository cache management

### CLI

- [ ] **CLI-01**: CLI framework setup (System.CommandLine or Spectre.Console.Cli)
- [ ] **CLI-02**: `issue` command group (list, create, update, delete, search, view)
- [ ] **CLI-03**: `agent` command group (list, create, update, delete, archive)
- [ ] **CLI-04**: `project` command group (list, create, update, delete)
- [ ] **CLI-05**: `workspace` command group (list, create, switch)
- [ ] **CLI-06**: `daemon` command group (start, stop, status, logs)
- [ ] **CLI-07**: `runtime` command group (list, create, update)
- [ ] **CLI-08**: `skill` command group (list, create, update, delete)
- [ ] **CLI-09**: `squad` command group (list, create, update, delete)
- [ ] **CLI-10**: `autopilot` command group (list, create, update, delete)
- [ ] **CLI-11**: `auth` command group (login, logout, status)
- [ ] **CLI-12**: `label` command group (list, create, update, delete)
- [ ] **CLI-13**: `config` command group (get, set)
- [ ] **CLI-14**: CLI self-update mechanism
- [ ] **CLI-15**: CLI configuration management

### Infrastructure & DevOps

- [ ] **INFRA-01**: Dockerfile for C# backend container
- [ ] **INFRA-02**: Docker Compose self-host configuration updated
- [ ] **INFRA-03**: Helm chart updated for C# backend
- [ ] **INFRA-04**: GitHub Actions CI pipeline updated for .NET
- [ ] **INFRA-05**: Prometheus metrics endpoint compatible with existing dashboards
- [ ] **INFRA-06**: Structured logging (Serilog) with JSON output

### API Compatibility

- [ ] **API-01**: All HTTP endpoints return identical JSON shapes to Go backend
- [ ] **API-02**: All HTTP status codes match Go backend behavior
- [ ] **API-03**: WebSocket message format matches existing TypeScript event types
- [ ] **API-04**: Cookie names, paths, and attributes match Go backend
- [ ] **API-05**: Header names and values match Go backend (X-Workspace-Slug, X-User-ID, etc.)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

- **PERF-01**: Performance benchmarking vs Go backend (latency, throughput, memory)
- **PERF-02**: Connection pool tuning for high-concurrency scenarios
- **OBS-01**: OpenTelemetry integration for distributed tracing
- **OBS-02**: Structured log aggregation (ELK/Loki)
- **SEC-01**: Security audit of C# implementation
- **SEC-02**: Penetration testing

## Out of Scope

| Feature | Reason |
|---------|--------|
| Frontend modifications | Frontend stays TypeScript/React — only backend changes |
| Database schema changes | Migration is code-only, schema stays identical |
| New features | Focus is 1:1 port, not feature additions |
| Mobile app changes | Mobile app is independent, shares only types from core |
| Go CLI binary distribution | GoReleaser replaced by .NET tool distribution |
| Performance optimization | v1 goal is correctness; performance tuning is v2 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FND-01..06 | Phase 1: Foundation | Pending |
| AUTH-01..12 | Phase 2: Auth & Middleware | Pending |
| MW-01..07 | Phase 2: Auth & Middleware | Pending |
| ISSUE-01..12 | Phase 3: Issues & Comments | Pending |
| CMT-01..04 | Phase 3: Issues & Comments | Pending |
| AGT-01..06 | Phase 4: Agents, Skills, Runtimes | Pending |
| SKL-01..03 | Phase 4: Agents, Skills, Runtimes | Pending |
| RT-01..07 (runtimes) | Phase 4: Agents, Skills, Runtimes | Pending |
| WS-01..04 | Phase 5: Workspaces, Projects, Squads | Pending |
| PRJ-01..02 | Phase 5: Workspaces, Projects, Squads | Pending |
| SQD-01..03 | Phase 5: Workspaces, Projects, Squads | Pending |
| RT-01..06 (realtime) | Phase 6: Realtime | Pending |
| SVC-01..08 | Phase 7: Services & Events | Pending |
| FEAT-01..21 | Phase 8: Remaining Features | Pending |
| DMN-01..16 | Phase 9: Daemon | Pending |
| CLI-01..15 | Phase 10: CLI | Pending |
| INFRA-01..06 | Phase 11: Infrastructure | Pending |
| API-01..05 | All Phases (continuous) | Pending |

**Coverage:**
- v1 requirements: 121 total
- Mapped to phases: 121
- Unmapped: 0

---
*Requirements defined: 2026-05-28*
*Last updated: 2026-05-28 after initial definition*
