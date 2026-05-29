# Architecture

**Analysis Date:** 2026-05-27

## System Overview

Multica is a multi-platform AI-native task management platform with a Go backend and a TypeScript frontend monorepo serving three client apps (web, desktop, mobile).

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                        Client Applications                              │
├──────────────────────┬──────────────────────┬───────────────────────────┤
│   apps/web/          │   apps/desktop/      │   apps/mobile/            │
│   Next.js (App Router)│   Electron           │   Expo / React Native     │
│   SSR + CSR          │   (electron-vite)     │   (independent)           │
├──────────────────────┴──────────────────────┴───────────────────────────┤
│                    Shared Packages (pnpm workspaces)                    │
├──────────────────────┬──────────────────────┬───────────────────────────┤
│  packages/views/     │  packages/core/      │  packages/ui/             │
│  Business pages &    │  Headless logic,     │  Atomic UI components,    │
│  components          │  stores, API client  │  design tokens, styles    │
├──────────────────────┴──────────────────────┴───────────────────────────┤
│                            Go Backend                                   │
│                    server/ (Chi router, sqlc, gorilla/websocket)        │
├─────────────────────────────────────────────────────────────────────────┤
│                   PostgreSQL (pgvector) + Redis                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Package Dependency Graph

Dependency direction is strictly layered:

```text
apps/web ──────┐
               ├──→ packages/views/ ──→ packages/core/
apps/desktop ──┤                   └──→ packages/ui/
               │
               │    (core and ui are independent of each other)
               │
apps/mobile/ ──┘──→ packages/core/ (types + pure functions only, via `import type`)
```

**Hard rules:**
- `packages/views/` depends on `@multica/core` and `@multica/ui`. Never imports from `next/*` or `react-router-dom`.
- `packages/core/` has zero react-dom, zero localStorage (uses StorageAdapter), zero UI libraries. Contains shared Zustand stores.
- `packages/ui/` has zero `@multica/core` imports. Pure UI, no business logic.
- `apps/mobile/` is independent. Shares only types and pure functions from `@multica/core/` with `import type` for types (zero runtime coupling).
- `apps/web/platform/` is the only place for Next.js APIs (`next/navigation`).
- `apps/desktop/src/renderer/src/platform/` is the only place for react-router-dom navigation wiring.

## Internal Packages Pattern

All shared packages export raw `.ts`/`.tsx` files with no pre-compilation step. The consuming app's bundler compiles them directly. This gives zero-config HMR and instant go-to-definition.

**Package exports use `"type": "module"` and explicit `exports` map in `package.json`:**
- `packages/core/package.json` maps ~80+ subpath exports (e.g. `"./issues/queries": "./issues/queries.ts"`)
- `packages/ui/package.json` maps component, hook, style, and markdown exports
- `packages/views/package.json` maps ~50+ subpath exports for domain modules

**Workspace references use `"workspace:*"` for inter-package dependencies.**

**Version pinning uses pnpm catalog** defined in `pnpm-workspace.yaml`. All shared deps (React, TypeScript, Zustand, TanStack Query, Vitest, etc.) use `catalog:` references to guarantee a single version across all packages.

## State Management

The architecture relies on a strict split between server state and client state:

### Server State: TanStack Query
- All API-fetched data (issues, users, workspaces, inbox, agents, etc.) lives in the Query cache.
- WS events keep it fresh via invalidation; no polling, no `staleTime` workarounds.
- QueryClient is provided by `CoreProvider` in `packages/core/provider.tsx`.
- Query/mutation definitions live alongside domain modules: `packages/core/issues/queries.ts`, `packages/core/issues/mutations.ts`.

### Client State: Zustand
- UI selections, filters, drafts, modal state, navigation history.
- Stores live in `packages/core/` (never in `packages/views/`), organized by domain:
  - `packages/core/issues/stores/` — issue view filters, view modes
  - `packages/core/agents/stores/` — agent-related UI state
  - `packages/core/squads/stores/` — squad-related UI state
  - `packages/core/projects/stores/` — project-related UI state
- Auth store: `packages/core/auth/store.ts` — created via factory + injected dependencies, registered by the platform layer.
- Chat store: managed via `createChatStore` in `packages/core/chat/`.

### React Context
- Reserved for cross-cutting platform plumbing only: `WorkspaceIdProvider`, `NavigationProvider`, `I18nProvider`.
- Not used for general state management.

### Key invariants:
- Never duplicate server data into Zustand.
- Workspace-scoped queries must key on `wsId`.
- Mutations are optimistic by default.
- WS events invalidate queries — never write to stores directly.
- Persist what's worth preserving across restarts; don't persist ephemeral UI state.

## Platform Bridge

Each app wraps its root with `<CoreProvider>` from `packages/core/platform/core-provider.tsx` and provides its own `NavigationAdapter` for routing.

**CoreProvider** (`packages/core/platform/core-provider.tsx`):
- Initializes `ApiClient` (HTTP client with auth headers, CSRF, workspace slug, client identity)
- Creates and registers auth store (`createAuthStore`) and chat store (`createChatStore`)
- Wraps children in `QueryProvider` → `AuthInitializer` → `WSProvider` → `I18nProvider`
- Module-level singletons — created once at first render, never recreated

**NavigationAdapter:**
- Web: `apps/web/platform/navigation.tsx` — uses Next.js `useRouter()`
- Desktop: `apps/desktop/src/renderer/src/platform/navigation.tsx` — uses react-router-dom `useNavigate()`
- Shared code uses `useNavigation().push()` from `packages/core/navigation/` — never framework-specific APIs

**Workspace identity** (`packages/core/platform/workspace-storage.ts`):
- `setCurrentWorkspace(slug, wsId)` is the single source of truth for active workspace
- Workspace-scoped storage namespaces keys with current slug via `createWorkspaceAwareStorage`
- Subscribers notified on workspace change (WS reconnect, persist store rehydration)

## Multi-Tenancy

- All database queries filter by `workspace_id` via sqlc parameter injection.
- Membership checks gate access at the middleware level (`middleware.RequireWorkspaceMember`).
- `X-Workspace-Slug` header routes requests to the correct workspace (read by `ApiClient.authHeaders()`).
- Workspace-scoped routes in the Go router use `middleware.RequireWorkspaceMember(queries)` or `middleware.RequireWorkspaceRoleFromURL(queries, "id", "owner", "admin")`.
- Worktree support: each worktree gets its own DB name and unique ports via `.env.worktree`.

## Agent Assignees

Assignees are polymorphic — can be a member or an agent:
- `assignee_type` + `assignee_id` on issues (not a single user FK).
- Agents render with distinct styling (purple background, robot icon).
- Agent presence derived from task snapshots: `packages/core/agents/derive-presence.ts`.

## Backend Architecture

**Go backend** in `server/` using standard Go project layout:

**Router:** Chi router (`github.com/go-chi/chi/v5`) with middleware chain:
- `middleware.RequestID`, `middleware.ClientMetadata`, `middleware.RequestLogger`
- `middleware.HTTPMetrics` (Prometheus), `middleware.Recoverer`
- `middleware.ContentSecurityPolicy`, CORS
- `middleware.Auth` / `middleware.DaemonAuth` for authentication
- `middleware.RequireWorkspaceMember` / `middleware.RequireWorkspaceRoleFromURL` for authorization
- Per-IP rate limiting via Redis

**Database:** PostgreSQL with pgvector extension. sqlc for type-safe query generation:
- SQL queries in `server/pkg/db/queries/*.sql`
- Generated Go code in `server/pkg/db/generated/`
- Migrations in `server/migrations/`

**Realtime:** gorilla/websocket for WebSocket connections:
- `server/internal/realtime/hub.go` — connection management, broadcasting
- `server/internal/realtime/redis_relay.go` — multi-node relay via Redis pub/sub
- `server/internal/realtime/sharded_stream_relay.go` — sharded event streaming

**Event bus:** `server/internal/events/bus.go` — internal pub/sub for decoupling handlers from side effects (notifications, activity logging, subscriber management).

**Daemon system:** `server/internal/daemon/` — local agent runtime management:
- Daemon registers, heartbeats, claims tasks, reports progress/completion
- WebSocket connection via `server/internal/daemonws/` hub
- Task lifecycle: queued → dispatched → running → completed/failed/cancelled
- Service layer: `server/internal/service/task.go` (87K lines) handles task orchestration

**Key internal packages:**
- `server/internal/handler/` — HTTP handlers (one file per domain: agent.go, issue.go, comment.go, etc.)
- `server/internal/middleware/` — auth, rate limiting, workspace scoping, request logging
- `server/internal/service/` — business logic (task orchestration, autopilot, email)
- `server/internal/storage/` — S3 and local file storage abstraction
- `server/internal/auth/` — JWT, PAT, daemon token, CloudFront signing
- `server/internal/analytics/` — PostHog integration
- `server/internal/metrics/` — Prometheus metrics

---

*Architecture analysis: 2026-05-27*
