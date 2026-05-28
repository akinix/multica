# Phase 2: Auth & Middleware - Context

**Gathered:** 2026-05-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Full authentication and authorization middleware chain matching Go backend. Delivers: JWT sessions, 4 token types (`mul_`, `mdt_`, `mat_`, `mcn_`), cookie auth with CSRF, Google OAuth, CloudFront signing, workspace role enforcement, rate limiting, and the general middleware pipeline (request ID, client metadata, logging, metrics, CSP).

**Requirements covered:** AUTH-01..12, MW-01..07

</domain>

<decisions>
## Implementation Decisions

### Auth Middleware Architecture
- **D-01:** 使用多个 AuthenticationScheme 方案组织认证。JWT 使用 ASP.NET Core 内置 JwtBearerAuthentication，其他 token 类型（PAT、daemon、task、cloud PAT）使用自定义 AuthenticationHandler。
- **D-02:** 采用前缀分发 scheme 方案：注册一个"总控" AuthenticationScheme，根据 Authorization header 的前缀（`Bearer`、`mul_`、`mdt_`、`mat_`、`mcn_`）分发到具体的 scheme。所有端点默认走这个 scheme，无需每个端点标注。
- **D-03:** 所有 token 类型验证成功后，映射到统一的 ClaimsPrincipal（包含 user_id、workspace_id、roles 等）。下游代码不关心 token 类型，只读 claims。

### Token 缓存策略
- **D-04:** 使用 StackExchange.Redis 做分布式缓存，存储 PAT、daemon token 和 workspace membership 的缓存。多实例共享缓存状态。
- **D-05:** 缓存 TTL 可配置，通过 appsettings.json 控制。默认值与 Go 一致：PAT/daemon token 5 分钟，membership 2 分钟。
- **D-06:** 采用主动失效策略：当 PAT 被撤销或用户被移出工作区时，主动删除 Redis 中的缓存条目。

### Google OAuth 流程
- **D-07:** 手动用 HttpClient 实现 Google OAuth 2.0 code exchange 流程，与 Go 实现完全一致。不使用 Microsoft.AspNetCore.Authentication.Google 内置中间件。
- **D-08:** 采用前端 popup + POST code 模式：前端用 popup 获取 Google authorization code，通过 POST 请求发送到后端 `/api/auth/google/callback`。
- **D-09:** 只使用 Google 返回的基本字段：email、name、picture、sub (Google ID)，与 Go 实现一致。

### 速率限制
- **D-10:** 用 StackExchange.Redis 实现自定义滑动窗口限流，与 Go 实现完全一致。不使用 ASP.NET Core 内置 Rate Limiting。
- **D-11:** 限流算法使用滑动窗口计数。
- **D-12:** 限流规则（每端点的限制数、窗口大小）写在 appsettings.json 中，运行时可修改。
- **D-13:** 超出速率限制时返回 HTTP 429 Too Many Requests，body 包含 retry_after 字段，与 Go 实现一致。

### Claude's Discretion
- CloudFront 签名实现：使用 AWS SDK for .NET（AWSSDK.CloudFront）实现 RSA-SHA1 签名，通过 AWS Secrets Manager 获取私钥。
- CSRF 保护：自定义 HMAC-bound CSRF token 中间件，与 Go 实现保持一致的 cookie 和 header 名称。
- 中间件顺序：遵循 Go 的中间件顺序（RequestID → ClientMetadata → RequestLogger → HTTPMetrics → Recoverer → CSP → CORS → Auth → Workspace）。

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Go Auth Implementation
- `server/internal/auth/jwt.go` — JWT token generation and validation (HMAC-SHA256)
- `server/internal/auth/cookie.go` — Cookie auth with HttpOnly + HMAC-bound CSRF
- `server/internal/auth/cloud_pat.go` — Cloud PAT (`mcn_`) validation against Multica Cloud Fleet
- `server/internal/auth/cloudfront.go` — CloudFront signed cookie/URL generation (RSA-SHA1)
- `server/internal/auth/pat_cache.go` — PAT token cache with SHA-256 hash lookup
- `server/internal/auth/daemon_token_cache.go` — Daemon token cache
- `server/internal/auth/membership_cache.go` — Workspace membership cache

### Go Middleware Implementation
- `server/internal/middleware/auth.go` — Auth middleware with multi-token-prefix routing
- `server/internal/middleware/daemon_auth.go` — Daemon authentication middleware
- `server/internal/middleware/workspace.go` — Workspace membership/role middleware
- `server/internal/middleware/ratelimit.go` — Per-IP rate limiting via Redis
- `server/internal/middleware/client.go` — Client metadata extraction (X-Client-Platform, X-Client-Version, X-Client-OS)
- `server/internal/middleware/request_logger.go` — Structured request logging with slow-request detection
- `server/internal/middleware/csp.go` — Content-Security-Policy header middleware
- `server/internal/middleware/cloudfront.go` — CloudFront middleware

### Requirements
- `.planning/REQUIREMENTS.md` — AUTH-01..12, MW-01..07 requirements
- `.planning/ROADMAP.md` — Phase 2 tasks and verification criteria

### Existing C# Foundation
- `server/src/Multica.Api/Program.cs` — Current middleware pipeline (Serilog, CORS, ExceptionHandler, health checks)
- `server/src/Multica.Infrastructure/` — DbContext, Redis, DI configuration

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **StackExchange.Redis connection** — Already configured in Phase 1 (`Multica.Infrastructure`), ready for distributed caching and rate limiting
- **Serilog pipeline** — Request logging middleware exists, can extend with client metadata enrichment
- **CORS middleware** — Basic CORS configured in Program.cs, needs origin configuration from appsettings
- **Health check endpoints** — Already implemented, no changes needed

### Established Patterns
- **ASP.NET Core Minimal API** — Use `app.MapGet/MapPost` pattern for new endpoints (e.g., `/api/auth/google/callback`)
- **EF Core DbContext** — All 49 entities mapped, use for user/PAT/workspace queries
- **DI registration pattern** — Services registered via `builder.Services.AddInfrastructure()`, extend for auth services

### Integration Points
- **Program.cs middleware pipeline** — Insert auth middleware after CORS, before endpoints
- **appsettings.json** — Add auth configuration sections (JWT secret, Google OAuth credentials, rate limit rules, cache TTLs)
- **EF Core DbContext** — Query users, PATs, workspace memberships for token validation

</code_context>

<specifics>
## Specific Ideas

- Token 前缀必须保持一致：`mul_` (PAT), `mdt_` (daemon token), `mat_` (task token), `mcn_` (cloud PAT)
- Cookie 名称必须保持一致：`multica_auth` (HttpOnly auth cookie)
- Header 名称必须保持一致：`X-Workspace-Slug`, `X-User-ID`, `X-Client-Platform`, `X-Client-Version`, `X-Client-OS`
- CSRF token 格式和验证逻辑必须与 Go 实现完全一致，确保前端无需修改

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 2-Auth & Middleware*
*Context gathered: 2026-05-28*
