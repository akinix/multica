# Codebase Concerns

**Analysis Date:** 2026-05-27

## Tech Debt

**Multi-node in-memory store divergence:**
- Issue: `UpdateStore`, `ModelListStore`, `LocalSkillListStore`, `LocalSkillImportStore` in `server/internal/handler/` all have in-memory implementations used by default and Redis-backed implementations for multi-node. The code comments explicitly warn that in-memory variants break multi-node deploys (see comment at `server/internal/handler/runtime_models.go:26-27` referencing `multica-ai/multica#2009`).
- Files: `server/internal/handler/runtime_update.go`, `server/internal/handler/runtime_models.go`, `server/internal/handler/runtime_local_skills.go`, `server/internal/handler/runtime_models_redis_store.go`, `server/internal/handler/runtime_local_skills_redis_store.go`
- Impact: Self-hosted single-node works; multi-node silently breaks pending-request flows (model lists, local skill imports, CLI updates). A wrong deployment config causes "No models available" with no clear error.
- Fix approach: Default to Redis when `REDIS_URL` is set; log a loud warning when running in-memory stores alongside multiple API replicas.

**Workspace slug-to-UUID lookup not cached:**
- Issue: Every request that uses `X-Workspace-Slug` header performs a DB lookup via `GetWorkspaceBySlug`. The code itself has `// TODO: cache slug→UUID lookup (slug is immutable, safe to cache with short TTL)` at `server/internal/middleware/workspace.go:112`.
- Files: `server/internal/middleware/workspace.go`
- Impact: Extra DB round-trip on every workspace-scoped request. At scale this becomes a hot-path bottleneck.
- Fix approach: Add an in-memory LRU cache with short TTL (e.g. 5 minutes) for the slug→UUID mapping, or reuse the Redis PAT cache infrastructure.

**Label name charset unvalidated:**
- Issue: Label names accept arbitrary strings including newlines, tabs, and control characters. The handler has `// TODO(labels): consider restricting to a charset that excludes newlines` at `server/internal/handler/label.go:93`.
- Files: `server/internal/handler/label.go`
- Impact: Labels with control characters can cause rendering issues in UI, break text-based exports, and potentially enable injection in contexts that don't escape control chars.
- Fix approach: Add a regex validation that excludes control characters (allow printable Unicode including emoji).

**Kiro agent dual payload field:**
- Issue: The Kiro agent backend has `// TODO: drop one field once Kiro lands on a single canonical payload` at `server/pkg/agent/kiro.go:270`.
- Files: `server/pkg/agent/kiro.go`
- Impact: Minor maintenance burden; dual fields risk drift.
- Fix approach: Coordinate with Kiro CLI team to settle on one payload shape, then remove the deprecated field.

**Text preview extension list duplicated across Go and TypeScript:**
- Issue: The set of text-previewable file extensions is maintained independently in `packages/views/editor/utils/preview.ts` (TEXT_EXTENSIONS) and `server/internal/handler/file.go` (isTextPreviewable). A comment at `packages/views/editor/utils/preview.ts:89` notes `// TODO(follow-up): extract to a JSON single-source-of-truth + generator`.
- Files: `packages/views/editor/utils/preview.ts`, `server/internal/handler/file.go`
- Impact: Adding a new previewable extension requires updating both files. If only one is updated, the user sees a 415 fallback or the Eye button doesn't appear.
- Fix approach: Extract to a shared JSON file (following the reserved-slugs pattern) and generate both the Go and TS sets.

**Large handler file sizes:**
- Issue: Several Go handler files exceed 1000 lines, with `server/internal/handler/issue.go` at 3098 lines and `server/internal/handler/daemon.go` at 2307 lines. The Handler struct in `server/internal/handler/handler.go` has 22+ fields.
- Files: `server/internal/handler/issue.go` (3098 lines), `server/internal/handler/daemon.go` (2307 lines), `server/internal/handler/autopilot.go` (1309 lines), `server/internal/handler/github.go` (1173 lines)
- Impact: Harder to navigate, higher cognitive load, more merge conflicts.
- Fix approach: Extract domain-specific handler groups into sub-handlers or service objects. The issue handler is the biggest candidate.

**Large frontend component sizes:**
- Issue: `packages/views/issues/components/issue-detail.tsx` is 2079 lines, `packages/core/api/client.ts` is 1865 lines, and `packages/views/issues/components/swimlane-view.tsx` is 1489 lines.
- Files: `packages/views/issues/components/issue-detail.tsx`, `packages/core/api/client.ts`, `packages/views/issues/components/swimlane-view.tsx`
- Impact: Harder to maintain, test in isolation, and reason about. The issue-detail component imports from 30+ modules.
- Fix approach: Split issue-detail into sub-components (sidebar, timeline, header, comments section). Split API client into domain-specific modules.

**Realtime sync handler registration:**
- Issue: `packages/core/realtime/use-realtime-sync.ts` (1014 lines) registers 30+ individual WS event handlers in a single useEffect with manual unsubscribe cleanup. Missing any unsubscribe causes a memory leak.
- Files: `packages/core/realtime/use-realtime-sync.ts`
- Impact: Fragile cleanup; easy to miss a new event handler's unsubscribe. The function is a single monolithic hook.
- Fix approach: Refactor into per-domain sync hooks (issue-sync, inbox-sync, chat-sync) composed together, each managing its own lifecycle.

**Mobile API client is a parallel implementation:**
- Issue: `apps/mobile/data/api.ts` (1270 lines) reimplements the API surface independently from `packages/core/api/client.ts` (1865 lines). Both must stay in sync with the backend contract.
- Files: `apps/mobile/data/api.ts`, `packages/core/api/client.ts`
- Impact: Any new endpoint or response shape change requires updating two files. The mobile CLAUDE.md explicitly documents the inbox dedup incident (2026-05-09) where mobile skipped preprocessing that web does.
- Fix approach: This is by design (mobile shares only types + pure functions). Accept the maintenance cost; mitigate with clear documentation of the "must-agree points" pattern already established.

## Known Bugs

**No open known bugs detected from code comments.**
- The codebase references past incidents (#2143, #2147, #2192, #2009, #1661, MUL-2600, MUL-2538) but all appear to be resolved. The lessons are encoded as defensive code patterns and documentation.

## Security Considerations

**JWT default secret in development:**
- Risk: `server/internal/auth/jwt.go:12` defines `defaultJWTSecret = "multica-dev-secret-change-in-production"`. If `JWT_SECRET` env var is not set in production, all tokens use this predictable secret.
- Files: `server/internal/auth/jwt.go`
- Current mitigation: The constant name is a clear warning; production deployments should set `JWT_SECRET`.
- Recommendations: Add a startup check that refuses to start with the default secret when `GO_ENV=production` or equivalent.

**Webhook rate limiting is per-IP only for IP limiter:**
- Risk: The `WebhookIPRateLimiter` uses `TrustedProxies` config to determine real client IP. If `MULTICA_TRUSTED_PROXIES` is misconfigured, attackers can bypass IP-based rate limiting via spoofed `X-Forwarded-For`.
- Files: `server/internal/handler/handler.go:105-106`, `server/internal/middleware/workspace.go`
- Current mitigation: Empty `TrustedProxies` defaults to "trust nothing" (uses `RemoteAddr`), which is safe. Code comments at `server/internal/handler/handler.go:76-80` explain the design.
- Recommendations: Document the `MULTICA_TRUSTED_PROXIES` setup requirement prominently for production deployments.

**CSRF protection is cookie-auth only:**
- Risk: CSRF validation (`auth.ValidateCSRF`) only applies to cookie-based auth. Bearer token auth (PAT, daemon token) bypasses CSRF. This is correct by design (bearer tokens aren't auto-attached by browsers) but worth noting.
- Files: `server/internal/middleware/auth.go:56-59`
- Current mitigation: Design is correct; cookies require CSRF, bearer tokens don't need it.
- Recommendations: No action needed; the design is sound.

**S3 bucket name misconfiguration detection:**
- Risk: `server/internal/storage/s3.go` warns when `S3_BUCKET` looks like a hostname rather than a bucket name, but doesn't refuse to start. Misconfigured bucket names cause silent upload failures.
- Files: `server/internal/storage/s3.go`
- Current mitigation: Warning log at startup.
- Recommendations: Consider failing fast (or at least logging at ERROR level) when the bucket name pattern is clearly wrong.

**Email subject injection protection:**
- Risk: `server/internal/service/email.go` caps user-controlled text in email subjects at 60 runes (`maxSubjectFieldRunes`) to prevent phishing pitch stuffing via workspace names.
- Files: `server/internal/service/email.go:24`
- Current mitigation: Truncation + HTML escaping in email body construction.
- Recommendations: The mitigation is adequate; document the constraint for future email template changes.

**Content Security Policy:**
- Risk: CSP header (`server/internal/middleware/csp.go`) uses `'unsafe-inline'` for styles, which weakens XSS protection. This is necessary for Tailwind/shadcn but reduces defense-in-depth.
- Files: `server/internal/middleware/csp.go`
- Current mitigation: All other directives are restrictive (no `unsafe-eval`, `frame-ancestors 'none'`, etc.).
- Recommendations: Consider migrating to nonce-based inline style allowlisting if feasible, though this is low priority given the other protections in place.

**File upload size limits consistent:**
- Both client (`packages/core/constants/upload.ts`: 100MB) and server (`server/internal/handler/file.go:30`: 100MB) enforce the same limit. Text preview is capped at 2MB server-side.
- Files: `packages/core/constants/upload.ts`, `server/internal/handler/file.go`
- Current mitigation: Client-side check prevents unnecessary upload; server-side `MaxBytesReader` is the authoritative gate.

## Performance Concerns

**Issue list query complexity:**
- Problem: The `ListIssues` SQL query (`server/pkg/db/queries/issue.sql`) contains a complex `involves_user_id` filter with three UNION subqueries joining across `agent`, `squad`, `squad_member` tables. This runs on every "my issues" or filtered issue list request.
- Files: `server/pkg/db/queries/issue.sql`
- Cause: Polymorphic assignee model (member/agent/squad) requires multi-table joins to resolve "issues involving user."
- Improvement path: Add composite indexes on `(workspace_id, assignee_type, assignee_id)` and ensure the subquery plans use them. Consider materializing the "user's agent IDs" and "user's squad IDs" sets in application code for the common case.

**No workspace slug cache:**
- Problem: Every request with `X-Workspace-Slug` header hits the DB for `GetWorkspaceBySlug`. With high request volume this becomes a hot path.
- Files: `server/internal/middleware/workspace.go`
- Cause: Explicitly noted as uncached in the code comments.
- Improvement path: Add an LRU cache with 5-minute TTL. The slug is immutable once created, so stale reads are impossible.

**Realtime hub is single-goroutine broadcast:**
- Problem: The `Hub` struct (`server/internal/realtime/hub.go:179`) processes register/unregister/broadcast through a single channel, creating a serialization bottleneck at high connection counts.
- Files: `server/internal/realtime/hub.go`
- Cause: Classic channel-based hub pattern; simple but single-threaded.
- Improvement path: The `ShardedStreamRelay` (`server/internal/realtime/sharded_stream_relay.go`) exists for multi-node scaling. For single-node, the current pattern is likely adequate until thousands of concurrent WS connections.

**Frontend bundle with heavy dependencies:**
- Problem: `packages/views/issues/components/issue-detail.tsx` imports from 30+ modules. The rich text editor (Tiptap with 12+ extensions), chart library (Recharts), emoji picker (emoji-mart), and virtualized list (react-virtuoso) are all loaded.
- Files: `packages/views/issues/components/issue-detail.tsx`, `apps/web/package.json`
- Cause: Feature-rich UI requires many libraries.
- Improvement path: Ensure code splitting is working (Next.js dynamic imports for heavy components like the editor, charts). Monitor bundle size with CI checks.

**Query invalidation cascade on issue updates:**
- Problem: `onIssueUpdated` in `packages/core/issues/ws-updaters.ts` invalidates issue list, my-issues, assignee groups, project Gantt, and parent/child caches on every issue update. This can trigger multiple refetches.
- Files: `packages/core/issues/ws-updaters.ts`
- Cause: Correctness-first approach — any field change can affect multiple views.
- Improvement path: Fine-grained invalidation based on which fields changed (e.g., only invalidate Gantt if `start_date` or `due_date` changed).

## API Compatibility Risks

**Desktop app version drift (documented incidents):**
- Risk: The desktop Electron app is an installed binary that cannot auto-update instantly. Users on older versions talk to newer backends. Three documented incidents: #2143, #2147, #2192 — all white-screen crashes from API response shape changes.
- Files: `packages/core/api/schema.ts`, `packages/core/api/schemas.ts`
- Current mitigation: `parseWithFallback` with Zod schemas and explicit fallbacks. All schemas use `.loose()` to pass unknown fields. Lenient schemas (string instead of enum, null unions with fallbacks). The `CLAUDE.md` "API Response Compatibility" section is comprehensive.
- Recommendations: Continue the pattern. Every new endpoint consumed by UI needs a schema + test. The schema test pattern (`packages/core/api/schemas.test.ts`) should be extended to cover more endpoints.

**Mobile API surface is independently maintained:**
- Risk: `apps/mobile/data/api.ts` is a parallel API client. If a backend response shape changes and only the shared `packages/core/api/schemas.ts` is updated, mobile won't pick up the fix.
- Files: `apps/mobile/data/api.ts`, `packages/core/api/schemas.ts`
- Current mitigation: Mobile imports schemas from `@multica/core/api/schemas` for endpoints that have them. The mobile CLAUDE.md documents the "must-agree points" pattern.
- Recommendations: Track which mobile endpoints have Zod coverage vs. bare type casts. Prioritize adding schemas to the most-used mobile endpoints.

**Enum drift defense:**
- Risk: Server-side enums (issue status, priority, assignee type) can gain new values. Frontend switch statements without `default` branches will silently drop new values.
- Files: `packages/core/issues/config/status.ts`, `packages/core/issues/config/priority.ts`
- Current mitigation: CLAUDE.md requires `default` branches on all server-driven string switches. Schemas use `z.string()` instead of `z.enum()` to avoid parse failures.
- Recommendations: Audit all switch statements on server-driven strings periodically.

## Cross-Platform Consistency

**Mobile test coverage is zero:**
- Risk: `apps/mobile/` has 235 TypeScript source files and 0 test files. Behavioral parity bugs (like the inbox dedup incident of 2026-05-09) are discovered only in production.
- Files: `apps/mobile/`
- Current mitigation: The mobile CLAUDE.md's "Pre-flight" checklist and "must-agree points" documentation.
- Recommendations: Add unit tests for `apps/mobile/lib/inbox-display.ts`, `apps/mobile/lib/issue-status.ts`, `apps/mobile/lib/format-activity.ts`, and other display-transform functions. These are the highest-risk parity gaps.

**Web app has minimal test coverage:**
- Risk: `apps/web/` has 91 TypeScript source files but only 4 test files. Platform-specific wiring (cookies, redirects, searchParams) is largely untested.
- Files: `apps/web/`
- Current mitigation: E2E tests cover auth and navigation flows.
- Recommendations: Add unit tests for `apps/web/config/runtime-urls.ts` (already has a test), `apps/web/lib/locale-routing.ts` (already has a test), and the auth callback flow.

**React version divergence:**
- Risk: Web/desktop use React 19.2.3 (from catalog), mobile uses React 19.1 (pinned in `apps/mobile/package.json`). This is intentional (Expo SDK pins its React version) but means hooks behavior can subtly differ.
- Files: `pnpm-workspace.yaml`, `apps/mobile/package.json`
- Current mitigation: Mobile shares only types and pure functions, not React components or hooks.
- Recommendations: Monitor for React 19.1 vs 19.2 behavioral differences in shared pure functions.

**Tailwind version divergence:**
- Risk: Web/desktop use Tailwind v4 (from catalog), mobile uses Tailwind 3.4 (NativeWind 4 constraint). Class names and configuration syntax differ.
- Files: `apps/mobile/CLAUDE.md`, `pnpm-workspace.yaml`
- Current mitigation: Mobile is fully independent for styling; no shared CSS.
- Recommendations: No action needed while mobile remains independent.

**Desktop route categories complexity:**
- Risk: Desktop has three route categories (session routes, transition flows, error/stale states) with different rendering paths. Choosing the wrong category reproduces fixed bugs. Transition flows are NOT routes — they're `WindowOverlay` state.
- Files: `apps/desktop/src/renderer/src/stores/tab-store.ts`, `apps/desktop/src/renderer/src/routes.tsx`, `apps/desktop/src/renderer/src/platform/navigation.tsx`
- Current mitigation: CLAUDE.md documents the rules explicitly with incident references.
- Recommendations: Add a comment in `routes.tsx` listing all overlay types so new contributors can find the complete set.

## Testing Gaps

**Go test coverage is strong but integration-heavy:**
- The Go backend has 188 test files (74,901 lines) covering 253 source files (96,611 lines). Tests require a live PostgreSQL database (`server/internal/handler/handler_test.go` connects to `DATABASE_URL`). This means tests can't run in isolation without a DB.
- Files: `server/internal/handler/handler_test.go`
- Risk: CI requires a PostgreSQL service; local dev without `make db-up` skips all Go tests silently (`os.Exit(0)` on connection failure).

**E2E coverage is thin:**
- Only 7 E2E specs cover: auth, chat-attachments, comments, issues, navigation, onboarding-v2-smoke, settings.
- Files: `e2e/`
- Risk: Major user flows like agent creation, autopilot management, workspace settings, squad management, and dashboard have no E2E coverage.
- Recommendations: Prioritize E2E tests for agent task lifecycle (create issue, assign to agent, task completes) and autopilot trigger flow.

**No mobile tests:**
- 0 test files in `apps/mobile/` for 235 source files.
- Files: `apps/mobile/`
- Risk: Display transform functions (inbox dedup, timeline coalescing, status formatting) have no test coverage.
- Recommendations: Start with `apps/mobile/lib/inbox-display.ts` and `apps/mobile/lib/issue-status.ts`.

**Shared packages have good coverage:**
- `packages/core/` has 43 test files for 218 source files (~20% file coverage).
- `packages/views/` has 93 test files for 439 source files (~21% file coverage).
- Risk: Coverage is decent but not comprehensive. Complex components like `issue-detail.tsx` (2079 lines) have a test file (`issue-detail.test.tsx`, 1113 lines) but the test-to-code ratio suggests many paths are untested.

## Dependency Health

**Go dependencies appear current:**
- Go 1.26.1, Chi v5.2.5, pgx v5.9.2, gorilla/websocket v1.5.3, AWS SDK v2, Redis client v9.18.0, Prometheus client v1.23.2.
- All dependencies are on current major versions. No deprecated libraries detected.

**Frontend dependencies are modern:**
- React 19.2.3, Next.js 16.2.5, TanStack Query 5.96.2, Zustand 5, Zod 4.1.5, Tailwind 4, TypeScript 5.9.3.
- All on latest major versions. The pnpm catalog ensures single versions across all packages.
- Risk: Next.js 16 + Turbopack has a known incompatibility with fumadocs-mdx (noted in `apps/web/next.config.ts:72-74`). The `--webpack` flag is a workaround.

**Mobile dependencies are pinned separately:**
- Expo SDK 55, React Native 0.83.6, React 19.1 (not from catalog).
- Risk: Intentional divergence but must be tracked independently.

**Desktop Electron version:**
- Electron is listed in `onlyBuiltDependencies` but the exact version is in `apps/desktop/package.json` devDependencies.
- Risk: Electron auto-update (`electron-updater` v6.8.3) handles desktop updates, but users on very old versions may not get the update prompt.

## Scaling Considerations

**Single-node in-memory stores:**
- Current capacity: In-memory stores for model lists, local skills, CLI updates, and liveness are unbounded maps.
- Limit: Memory grows with number of active runtimes and pending requests. In multi-node deploys, these stores break (see Tech Debt section).
- Scaling path: Redis-backed implementations already exist for all stores. The router must wire them when `REDIS_URL` is set.

**WebSocket hub architecture:**
- Current capacity: Single-goroutine hub processes all register/unregister/broadcast. Adequate for hundreds of connections.
- Limit: At thousands of concurrent WS connections, the hub becomes a bottleneck.
- Scaling path: `ShardedStreamRelay` (`server/internal/realtime/sharded_stream_relay.go`) enables multi-node WS via Redis Streams with 8 shards and 100K stream max length.

**Database query patterns:**
- Current capacity: All queries filter by `workspace_id` (multi-tenant isolation). Proper indexes assumed from sqlc-generated code.
- Limit: The `ListIssues` query with `involves_user_id` subqueries may not scale well with large numbers of agents/squads per workspace.
- Scaling path: Monitor query plans; add covering indexes if needed.

**File storage:**
- Current capacity: S3-backed with CloudFront signing for downloads. 100MB upload limit, 2MB text preview limit.
- Limit: No per-workspace storage quotas detected.
- Scaling path: Add workspace-level storage quotas if needed for SaaS.

**Email delivery:**
- Current capacity: Resend API for transactional email; SMTP fallback configured.
- Limit: Resend rate limits apply; no queuing/retry for failed sends detected.
- Scaling path: Add email send queue with retry if volume increases.

## Recommendations

1. **High priority: Cache workspace slug-to-UUID lookups.** This is the most impactful performance improvement with the lowest implementation effort. Add an LRU cache with 5-minute TTL in `server/internal/middleware/workspace.go`.

2. **High priority: Add mobile tests for display transforms.** The inbox dedup incident (2026-05-09) demonstrates the risk. Start with `apps/mobile/lib/inbox-display.ts` and `apps/mobile/lib/issue-status.ts`.

3. **High priority: Guard against default JWT secret in production.** Add a startup check in `server/cmd/server/main.go` that warns or refuses to start when `JWT_SECRET` is the default value and the environment is production.

4. **Medium priority: Extract text preview extension list to shared JSON.** Follow the reserved-slugs pattern (`server/internal/handler/reserved_slugs.json` + generator) to eliminate the Go/TS duplication.

5. **Medium priority: Add E2E tests for agent task lifecycle.** The core product loop (create issue, assign to agent, agent completes task) has no E2E coverage.

6. **Medium priority: Split large handler files.** `server/internal/handler/issue.go` (3098 lines) and `server/internal/handler/daemon.go` (2307 lines) should be decomposed into smaller, domain-focused files.

7. **Medium priority: Split large frontend components.** `packages/views/issues/components/issue-detail.tsx` (2079 lines) should be decomposed into sub-components.

8. **Medium priority: Wire Redis stores by default when REDIS_URL is set.** The in-memory stores should not be the default when Redis is available; this prevents silent multi-node breakage.

9. **Low priority: Add label name charset validation.** Exclude control characters from label names in `server/internal/handler/label.go`.

10. **Low priority: Refactor realtime sync into per-domain hooks.** Split `packages/core/realtime/use-realtime-sync.ts` (1014 lines, 30+ event handlers) into composable per-domain sync hooks.

---

*Concerns audit: 2026-05-27*
