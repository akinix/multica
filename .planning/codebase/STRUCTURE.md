# Codebase Structure

**Analysis Date:** 2026-05-27

## Directory Layout

```
multica/
├── server/              # Go backend (Chi router, sqlc, gorilla/websocket)
├── apps/
│   ├── web/             # Next.js frontend (App Router)
│   ├── desktop/         # Electron desktop app (electron-vite)
│   ├── mobile/          # Expo / React Native iOS app
│   └── docs/            # Documentation site (fumadocs)
├── packages/
│   ├── core/            # Headless business logic (zero react-dom)
│   ├── ui/              # Atomic UI components (zero business logic)
│   ├── views/           # Shared business pages/components
│   ├── tsconfig/        # Shared TypeScript configuration
│   └── eslint-config/   # Shared ESLint configuration
├── e2e/                 # Playwright end-to-end tests
├── scripts/             # Build/utility scripts
├── deploy/              # Deployment configuration
├── docker/              # Docker configuration
├── docs/                # Legacy docs (redirect to apps/docs)
├── Makefile             # Dev/build/migrate commands
├── turbo.json           # Turborepo pipeline config
├── pnpm-workspace.yaml  # pnpm workspace + catalog versions
├── package.json         # Root package.json (scripts, devDeps)
├── playwright.config.ts # E2E test config
├── .goreleaser.yml      # GoReleaser config for CLI releases
├── Dockerfile           # Go server container
└── Dockerfile.web       # Next.js container
```

## server/ (Go Backend)

```
server/
├── cmd/
│   ├── server/          # Main API server entry point
│   │   ├── main.go      # Server startup, DB pool, Redis, Hub init
│   │   └── router.go    # Chi router with all middleware and route registration
│   ├── multica/         # CLI entry point (cobra)
│   ├── migrate/         # Database migration tool
│   └── backfill_task_usage_hourly/  # Data backfill utility
├── internal/
│   ├── handler/         # HTTP handlers (one file per domain)
│   │   ├── agent.go, agent_access.go, agent_env.go, agent_template.go
│   │   ├── auth.go
│   │   ├── issue.go, comment.go
│   │   ├── chat.go
│   │   ├── daemon.go, daemon_ws.go
│   │   ├── autopilot.go, autopilot_webhook.go
│   │   ├── runtime.go, cloud_runtime.go
│   │   ├── workspace.go, member.go, invitation.go
│   │   ├── project.go, project_resource.go
│   │   ├── skill.go, label.go, pin.go, squad.go
│   │   ├── inbox.go, notification_preference.go
│   │   ├── attachment.go, upload.go
│   │   ├── dashboard.go, activity.go
│   │   ├── contact_sales.go, feedback.go
│   │   ├── personal_access_token.go, config.go
│   │   ├── github.go
│   │   └── reserved_slugs.json  # Reserved workspace slugs
│   ├── middleware/       # HTTP middleware
│   │   ├── auth.go, daemon_auth.go  # JWT/PAT/daemon token auth
│   │   ├── workspace.go             # Workspace membership checks
│   │   ├── owner_lookup.go          # URL param → workspace resolver
│   │   ├── ratelimit.go             # Per-IP rate limiting (Redis)
│   │   ├── request_logger.go        # Structured request logging
│   │   ├── client.go                # X-Client-* header extraction
│   │   ├── csp.go, cloudfront.go    # Security headers
│   │   └── auth_test.go, workspace_test.go, ...
│   ├── service/         # Business logic layer
│   │   ├── task.go           # Task orchestration (87K — largest file)
│   │   ├── autopilot.go      # Autopilot execution engine
│   │   ├── email.go          # Email service (Resend)
│   │   ├── health.go         # Health/readiness checks
│   │   ├── listeners.go      # Event bus listeners
│   │   ├── notification_listeners.go  # Notification generation
│   │   ├── activity_listeners.go      # Activity logging
│   │   ├── subscriber_listeners.go    # Auto-subscription
│   │   ├── autopilot_listeners.go     # Autopilot trigger evaluation
│   │   ├── autopilot_failure_monitor.go  # Failure alerting
│   │   ├── runtime_sweeper.go         # Stale runtime cleanup
│   │   └── dbstats.go                 # DB connection pool stats
│   ├── realtime/        # WebSocket system
│   │   ├── hub.go              # Connection management, broadcasting
│   │   ├── broadcaster.go      # Event distribution
│   │   ├── redis_relay.go      # Multi-node relay via Redis pub/sub
│   │   ├── sharded_stream_relay.go  # Sharded event streaming
│   │   ├── relay_lifecycle.go  # Relay start/stop management
│   │   └── metrics.go          # WS connection metrics
│   ├── daemon/          # Local daemon runtime
│   │   ├── daemon.go         # Main daemon loop (128K)
│   │   ├── config.go         # Runtime configuration
│   │   ├── client.go         # Server API client
│   │   ├── prompt.go         # Agent prompt construction
│   │   ├── gc.go             # Garbage collection
│   │   ├── health.go         # Daemon health checks
│   │   ├── local_directory.go, local_skills.go
│   │   ├── identity.go, wakeup.go, poisoned.go
│   │   ├── execenv/          # Execution environment
│   │   └── repocache/        # Repository cache
│   ├── daemonws/        # Daemon WebSocket hub
│   ├── events/          # Internal event bus
│   │   ├── bus.go            # Pub/sub bus implementation
│   │   └── bus_test.go
│   ├── auth/            # Authentication utilities
│   ├── analytics/       # PostHog analytics client
│   ├── metrics/         # Prometheus metrics
│   ├── storage/         # S3 and local file storage
│   ├── mention/         # @mention parsing
│   ├── issueguard/      # Issue access control
│   ├── agenttmpl/       # Agent template engine
│   ├── cloudruntime/    # Cloud runtime fleet client
│   ├── logger/          # Structured logging (tint)
│   ├── migrations/      # Migration utilities
│   ├── cli/             # CLI command implementations
│   └── util/            # Shared utilities (UUID parsing, etc.)
├── pkg/
│   ├── db/
│   │   ├── generated/   # sqlc-generated Go code
│   │   └── queries/     # SQL query files (30+ files)
│   ├── agent/           # Agent protocol definitions
│   ├── protocol/        # Wire protocol types
│   └── redact/          # Secret redaction
├── migrations/          # SQL migration files
├── go.mod               # Go module definition
├── go.sum               # Go dependency checksums
└── sqlc.yaml            # sqlc code generation config
```

## apps/web/ (Next.js)

```
apps/web/
├── app/                 # Next.js App Router
│   ├── (auth)/          # Auth route group (login, etc.)
│   ├── (landing)/       # Landing page route group
│   ├── [workspaceSlug]/ # Workspace-scoped routes
│   │   ├── (dashboard)/ # Dashboard route group
│   │   │   ├── issues/
│   │   │   ├── projects/
│   │   │   ├── agents/
│   │   │   ├── squads/
│   │   │   ├── inbox/
│   │   │   ├── my-issues/
│   │   │   ├── skills/
│   │   │   ├── runtimes/
│   │   │   ├── autopilots/
│   │   │   ├── members/
│   │   │   ├── settings/
│   │   │   ├── usage/
│   │   │   ├── layout.tsx    # Dashboard layout
│   │   │   └── loading.tsx
│   │   ├── attachments/ # Attachment preview routes
│   │   └── layout.tsx   # Workspace layout (resolves slug, sets workspace context)
│   └── auth/            # OAuth callback routes
├── platform/
│   └── navigation.tsx   # Next.js NavigationAdapter (useRouter wrapper)
├── features/            # Web-only feature implementations
│   ├── auth/
│   └── landing/
├── components/          # Web-only components
├── config/              # App configuration
├── content/             # MDX content (use cases)
├── lib/                 # Web-only utilities
├── public/              # Static assets
├── test/                # Web-specific tests
├── package.json         # @multica/web
└── next.config.ts       # Next.js configuration
```

## apps/desktop/ (Electron)

```
apps/desktop/
├── src/
│   ├── main/            # Electron main process
│   │   ├── index.ts     # App entry, window creation
│   │   ├── daemon-manager.ts  # Local daemon lifecycle
│   │   ├── updater.ts   # Auto-update (electron-updater)
│   │   ├── cli-bootstrap.ts   # CLI binary management
│   │   ├── context-menu.ts    # Native context menus
│   │   ├── keyboard-shortcuts.ts  # Global shortcuts
│   │   └── ...
│   ├── renderer/        # Electron renderer process
│   │   └── src/
│   │       ├── App.tsx          # Root component with CoreProvider
│   │       ├── routes.tsx       # react-router-dom route definitions
│   │       ├── main.tsx         # Renderer entry point
│   │       ├── platform/        # Desktop NavigationAdapter (react-router-dom)
│   │       ├── components/      # Desktop-only components
│   │       ├── hooks/           # Desktop-only hooks
│   │       ├── pages/           # Desktop-only page wrappers
│   │       └── stores/          # Desktop-only stores (tab, window overlay)
│   ├── preload/         # Electron preload scripts
│   └── shared/          # Shared between main/renderer
├── build/               # Build assets (icons)
├── resources/           # App resources
├── scripts/             # Build scripts (package, brand, bundle-cli)
├── test/                # Desktop-specific tests
└── package.json         # @multica/desktop
```

## apps/mobile/ (Expo)

```
apps/mobile/
├── app/                 # Expo Router (file-based routing)
│   ├── (auth)/          # Auth screens (login)
│   ├── (app)/           # Authenticated screens
│   │   ├── [workspace]/ # Workspace-scoped screens
│   │   ├── _layout.tsx
│   │   └── select-workspace.tsx
│   └── _layout.tsx      # Root layout
├── components/          # Mobile-specific components
│   ├── brand/
│   ├── chat/
│   ├── composer/
│   ├── editor/
│   ├── inbox/
│   ├── issue/
│   ├── nav/
│   ├── project/
│   └── ui/
├── data/                # Mobile data layer
│   ├── queries/         # TanStack Query hooks
│   ├── mutations/       # Mutation hooks
│   ├── realtime/        # WebSocket integration
│   └── stores/          # Zustand stores (mobile-specific)
├── lib/                 # Mobile utilities
├── assets/              # Images, fonts
├── docs/                # Mobile-specific documentation
├── CLAUDE.md            # Mobile-specific rules
└── package.json         # @multica/mobile (pinned versions, not catalog)
```

## packages/core/

```
packages/core/
├── platform/            # CoreProvider, workspace storage, keyboard utils
│   ├── core-provider.tsx    # Root provider (API, auth, WS, Query, i18n)
│   ├── workspace-storage.ts # Workspace identity singleton
│   ├── auth-initializer.tsx # Auth state bootstrap
│   ├── storage.ts           # Default StorageAdapter
│   ├── persist-storage.ts   # Zustand persist storage factory
│   └── keyboard.ts          # Platform keyboard detection
├── api/                 # API client layer
│   ├── client.ts        # ApiClient class (all HTTP methods, 1800+ lines)
│   ├── schema.ts        # parseWithFallback (Zod validation)
│   ├── schemas.ts       # Zod schemas for API responses
│   ├── ws-client.ts     # WebSocket client
│   └── index.ts         # API instance singleton
├── auth/                # Auth store (Zustand)
│   ├── store.ts         # createAuthStore factory
│   ├── index.ts
│   └── utils.ts
├── issues/              # Issue domain
│   ├── queries.ts       # TanStack Query hooks
│   ├── mutations.ts     # Mutation hooks (optimistic updates)
│   ├── ws-updaters.ts   # WebSocket event → query cache updaters
│   ├── stores/          # Issue view stores (Zustand)
│   ├── config/          # Status, priority enums and configs
│   ├── cache-helpers.ts # Query key helpers
│   └── delete-cache.ts  # Cache cleanup on delete
├── chat/                # Chat domain
├── agents/              # Agent domain
│   ├── stores/          # Agent UI stores
│   ├── derive-presence.ts   # Agent presence derivation
│   └── use-agent-presence.ts
├── workspace/           # Workspace queries/mutations/hooks
├── projects/            # Project domain
│   ├── stores/          # Project UI stores
│   ├── queries.ts, mutations.ts
│   └── config.ts
├── squads/              # Squad domain
│   └── stores/          # Squad UI stores
├── runtimes/            # Runtime domain
├── autopilots/          # Autopilot domain
├── inbox/               # Inbox domain
├── labels/              # Label domain
├── pins/                # Pin domain
├── skills/              # Skill domain
├── dashboard/           # Dashboard queries
├── github/              # GitHub integration
├── feedback/            # Feedback submission
├── notification-preferences/  # Notification settings
├── onboarding/          # Onboarding flow
├── modals/              # Modal registry
├── navigation/          # Navigation store
│   └── store.ts         # useNavigationStore
├── permissions/         # Permission checks
├── paths/               # URL path helpers
│   └── reserved-slugs.ts  # Generated from server reserved_slugs.json
├── analytics/           # PostHog analytics
├── realtime/            # WebSocket provider and sync hooks
│   ├── provider.tsx     # WSProvider
│   ├── use-realtime-sync.ts  # Main sync hook (42K)
│   └── hooks.ts
├── config/              # App configuration
├── constants/           # Shared constants
├── hooks/               # Shared hooks
├── i18n/                # Internationalization (i18next)
├── logger/              # Structured logging
├── markdown/            # Markdown utilities
├── types/               # Shared TypeScript types
│   └── storage.ts       # StorageAdapter interface
├── utils.ts             # Shared utilities
├── query-client.ts      # QueryClient configuration
├── provider.tsx         # QueryProvider
└── index.ts             # Main barrel export
```

## packages/ui/

```
packages/ui/
├── components/
│   ├── ui/              # shadcn components (Base UI primitives)
│   │   ├── button.tsx, input.tsx, dialog.tsx, ...
│   │   └── (50+ components)
│   └── common/          # Shared non-shadcn components
│       └── error-boundary.tsx, ...
├── hooks/               # UI-only hooks
├── lib/
│   ├── utils.ts         # cn() utility (tailwind-merge + clsx)
│   └── data-table.ts    # DataTable helpers
├── markdown/            # Markdown rendering components
│   ├── index.ts
│   └── linkify.ts, mentions.ts
├── styles/              # CSS design tokens and base styles
│   ├── tokens.css       # Semantic color tokens
│   └── base.css         # Base layer styles
├── types/
│   └── i18next.ts       # i18n type definitions
├── components.json      # shadcn configuration (Base UI variant)
└── package.json         # @multica/ui
```

## packages/views/

```
packages/views/
├── layout/              # Dashboard layout components
│   ├── app-sidebar.tsx      # Main sidebar (29K)
│   ├── dashboard-guard.tsx  # Auth/workspace guard
│   ├── dashboard-layout.tsx # Layout wrapper
│   └── workspace-loader.tsx
├── issues/              # Issue views
│   ├── components/      # Issue list, detail, board views
│   ├── hooks/           # Issue-specific hooks
│   ├── actions/         # Issue action handlers
│   └── utils/           # Filter, sort utilities
├── projects/            # Project views
│   └── components/
├── agents/              # Agent views
│   └── components/
├── squads/              # Squad views
│   └── components/
├── chat/                # Chat views
│   ├── components/
│   └── lib/
├── editor/              # Rich text editor (Tiptap)
│   ├── extensions/      # Custom Tiptap extensions
│   ├── hooks/
│   ├── styles/
│   └── utils/
├── inbox/               # Inbox views
│   └── components/
├── dashboard/           # Dashboard views
│   └── components/
├── settings/            # Settings views
│   └── components/
├── skills/              # Skills views
│   ├── components/
│   ├── hooks/
│   └── lib/
├── runtimes/            # Runtime views
│   └── components/
├── autopilots/          # Autopilot views
│   └── components/
├── my-issues/           # My issues views
│   └── components/
├── search/              # Search views
├── labels/              # Label management
├── members/             # Member management
├── workspace/           # Workspace creation/settings views
├── auth/                # Auth views (login page)
├── onboarding/          # Onboarding flow
│   ├── components/
│   ├── steps/
│   └── templates/
├── invitations/         # Invitation views
├── invite/              # Invite acceptance
├── attachments/         # Attachment views
├── common/              # Shared view components
│   ├── actor-avatar.tsx
│   ├── markdown.tsx
│   └── task-transcript/
├── navigation/          # Navigation exports
├── platform/            # Platform-specific exports (DragStrip, etc.)
├── modals/              # Modal components
│   ├── registry.tsx     # Modal registry
│   └── create-issue.tsx
├── i18n/                # i18n hooks and exports
├── locales/             # Translation files
│   ├── en/              # English translations
│   └── zh-Hans/         # Simplified Chinese translations
├── test/                # Shared test utilities
└── package.json         # @multica/views
```

## Naming Conventions

**Files:**
- React components: `kebab-case.tsx` (e.g. `app-sidebar.tsx`, `dashboard-guard.tsx`)
- Hooks: `kebab-case.ts` prefixed with `use-` (e.g. `use-realtime-sync.ts`, `use-agent-presence.ts`)
- Stores: `kebab-case.ts` suffixed with `-store` (e.g. `view-store-context.tsx`)
- Queries/mutations: `queries.ts`, `mutations.ts` (domain-scoped by directory)
- Go files: `snake_case.go` (e.g. `agent_access.go`, `autopilot_webhook.go`)
- SQL files: `snake_case.sql` (e.g. `agent.sql`, `issue_label.sql`)

**Directories:**
- Packages: `kebab-case` (e.g. `packages/core/`, `packages/views/`)
- Domain modules: `kebab-case` (e.g. `packages/core/issues/`, `packages/views/agents/`)
- Go packages: `lowercase` single word (e.g. `handler/`, `middleware/`, `realtime/`)

**Components:**
- PascalCase for React components
- camelCase for hooks, utilities, stores

## Where to Add New Code

**New shared page/feature:**
- Implementation: `packages/views/<domain>/components/`
- Hooks: `packages/views/<domain>/hooks/`
- Route wiring (web): `apps/web/app/[workspaceSlug]/(dashboard)/<domain>/`
- Route wiring (desktop): `apps/desktop/src/renderer/src/routes.tsx`

**New shared store (client state):**
- Store: `packages/core/<domain>/stores/`
- Export from `packages/core/<domain>/index.ts`

**New API query/mutation:**
- Queries: `packages/core/<domain>/queries.ts`
- Mutations: `packages/core/<domain>/mutations.ts`
- Zod schema: `packages/core/api/schemas.ts`

**New UI component (no business logic):**
- Component: `packages/ui/components/ui/`
- Install via: `pnpm ui:add <component-name>`

**New Go handler:**
- Handler: `server/internal/handler/<domain>.go`
- SQL queries: `server/pkg/db/queries/<domain>.sql`
- Generate: `make sqlc`

**New E2E test:**
- Test: `e2e/<feature>.spec.ts`

## Special Directories

**`server/pkg/db/generated/`:**
- Purpose: sqlc-generated Go code from SQL queries
- Generated: Yes (`make sqlc`)
- Committed: Yes

**`packages/core/paths/reserved-slugs.ts`:**
- Purpose: Generated TypeScript list of reserved workspace slugs
- Generated: Yes (`pnpm generate:reserved-slugs`)
- Committed: Yes (CI checks for drift)

**`server/migrations/`:**
- Purpose: Database migration SQL files
- Generated: Manual
- Committed: Yes

**`.planning/`:**
- Purpose: GSD planning documents
- Generated: Yes
- Committed: No (gitignored)

---

*Structure analysis: 2026-05-27*
