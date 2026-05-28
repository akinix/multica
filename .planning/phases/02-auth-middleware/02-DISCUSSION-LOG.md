# Phase 2: Auth & Middleware - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-28
**Phase:** 2-Auth & Middleware
**Areas discussed:** 认证中间件架构, Token缓存策略, Google OAuth流程, 速率限制

---

## 认证中间件架构

### Q1: ASP.NET Core 中如何组织多 token 类型的认证？

| Option | Description | Selected |
|--------|-------------|----------|
| 单一认证方案 + 前缀分发 | 一个 AuthenticationHandler 处理所有 token 类型，根据前缀分发。最接近 Go 的实现方式，代码集中便于维护。 | |
| 多个认证方案 | 每种 token 类型注册为独立的 AuthenticationScheme。ASP.NET Core 原生方式，但需要多个 scheme 之间协调。 | ✓ |
| 自定义中间件（不用框架） | 完全自定义中间件，不使用 ASP.NET Core 认证框架。最灵活但失去框架提供的 [Authorize] 等特性。 | |

**User's choice:** 多个认证方案（推荐）
**Notes:** 选择 ASP.NET Core 原生方式，利用框架提供的认证机制。

### Q2: JWT 用 ASP.NET Core 内置的 JwtBearerAuthentication，还是所有 token 类型都用自定义 Handler？

| Option | Description | Selected |
|--------|-------------|----------|
| JWT 用内置 + 其他自定义 | JWT 用 JwtBearerHandler，其他 token 类型用自定义 AuthenticationHandler。JWT 利用框架内置能力，PAT/daemon/task token 自定义逻辑。 | ✓ |
| 全部自定义 Handler | 全部用自定义 AuthenticationHandler 实现，完全控制验证逻辑。一致性好，但 JWT 部分重复实现了框架已有功能。 | |

**User's choice:** JWT 用内置 + 其他自定义
**Notes:** 利用框架内置的 JWT 处理能力，减少重复代码。

### Q3: 多个 scheme 注册后，如何决定哪个端点用哪个 scheme？

| Option | Description | Selected |
|--------|-------------|----------|
| 前缀分发 scheme | 注册一个"总控"AuthenticationScheme，先检查 token 前缀再分发到具体 scheme。所有端点默认走这个 scheme，无需每个端点标注。 | ✓ |
| 端点级 scheme 注解 | 每个端点用 [Authorize(AuthenticationSchemes = "...")] 指定。精确但需要改每个 handler 的注解，迁移工作量大。 | |
| PolicyScheme 自动选择 | 用 PolicyScheme 根据请求自动选择 scheme。灵活但配置复杂。 | |

**User's choice:** 前缀分发 scheme（推荐）
**Notes:** 最接近 Go 的实现方式，迁移成本最低。

### Q4: 验证成功后，用户信息如何传递给下游 handler？

| Option | Description | Selected |
|--------|-------------|----------|
| 统一 ClaimsPrincipal | 所有 token 类型验证后都映射到统一的 ClaimsPrincipal（包含 user_id、workspace_id、roles 等）。下游代码不关心 token 类型，只读 claims。 | ✓ |
| 按 token 类型区分 Claims | 每种 token 类型生成不同的 ClaimsPrincipal 结构。下游需要根据 token 类型做不同处理。 | |

**User's choice:** 统一 ClaimsPrincipal（推荐）
**Notes:** 简化下游代码，统一处理逻辑。

---

## Token 缓存策略

### Q1: PAT、daemon token、membership 的缓存应该放在哪里？

| Option | Description | Selected |
|--------|-------------|----------|
| MemoryCache 内存缓存 | 使用 MemoryCache（ASP.NET Core 内置），单进程内存缓存。与 Go 实现最接近，简单高效，但多实例部署时各实例缓存独立。 | |
| Redis 分布式缓存 | 使用已配置的 StackExchange.Redis 做分布式缓存。多实例共享缓存，但增加 Redis 网络开销。 | ✓ |
| 混合方案 | PAT 和 daemon token 用 MemoryCache（高频访问），membership 用 Redis（需要跨实例一致性）。 | |

**User's choice:** Redis 分布式缓存
**Notes:** 选择分布式缓存以支持多实例部署场景。

### Q2: 缓存过期时间怎么设置？

| Option | Description | Selected |
|--------|-------------|----------|
| 沿用 Go 的 TTL | PAT/daemon token 缓存 5 分钟，membership 缓存 2 分钟。与 Go 代码的 TTL 保持一致。 | |
| 可配置 TTL | 根据 token 类型和访问频率动态调整 TTL，用配置文件控制。更灵活但增加复杂度。 | ✓ |

**User's choice:** 可配置 TTL
**Notes:** 通过 appsettings.json 配置，运行时可调整。

### Q3: 当 PAT 被撤销或用户被移出工作区时，缓存如何失效？

| Option | Description | Selected |
|--------|-------------|----------|
| 主动失效 | token 被撤销或 membership 变更时，主动删除 Redis 中的缓存条目。需要在相关操作中添加失效逻辑。 | ✓ |
| 仅 TTL 过期 | 依赖 TTL 自动过期，不做主动失效。简单但撤销后有延迟窗口。 | |

**User's choice:** 主动失效（推荐）
**Notes:** 确保撤销后立即生效，避免安全风险。

---

## Google OAuth 流程

### Q1: Google OAuth 的 code exchange 流程用什么方式实现？

| Option | Description | Selected |
|--------|-------------|----------|
| 内置 Google 认证中间件 | 使用 Microsoft.AspNetCore.Authentication.Google。内置完整 OAuth 流程，自动处理 code exchange、token 验证、用户信息获取。但回调路径和流程由框架控制，可能与 Go 的实现细节不完全一致。 | |
| 手动 HTTP 实现 | 手动用 HttpClient 实现 code exchange 和用户信息获取。与 Go 实现完全一致，精确控制每一步。但需要自己处理 token 验证和错误处理。 | ✓ |
| Google.Apis.Auth 库 | 用 Google.Apis.Auth 库处理 token 验证和用户信息，但自己控制 HTTP 流程。平衡了便利性和控制力。 | |

**User's choice:** 手动 HTTP 实现（推荐）
**Notes:** 确保与 Go 实现完全一致，避免前端需要修改。

### Q2: Google OAuth 的回调流程是怎样的？

| Option | Description | Selected |
|--------|-------------|----------|
| 前端 popup + POST code | 前端用 popup 模式，将 authorization code 通过 POST 请求发送到后端 /api/auth/google/callback。Go 的实现方式。 | ✓ |
| 后端直接处理 redirect | 后端直接处理 Google 的 redirect callback，前端通过 redirect URL 接收结果。标准 OAuth 流程。 | |

**User's choice:** 前端 popup + POST code（推荐）
**Notes:** 与 Go 实现一致，前端无需修改。

### Q3: 从 Google 获取用户信息时，需要哪些字段？

| Option | Description | Selected |
|--------|-------------|----------|
| 基本字段 | 只使用 Google 返回的基本字段：email、name、picture、sub (Google ID)。与 Go 实现一致。 | ✓ |
| 扩展字段 | 额外请求 email_verified、locale 等字段，为将来的功能扩展做准备。 | |

**User's choice:** 基本字段（与 Go 一致）
**Notes:** 保持与 Go 实现完全一致。

---

## 速率限制

### Q1: 速率限制用 ASP.NET Core 内置的 Rate Limiting，还是用 Redis 自定义实现？

| Option | Description | Selected |
|--------|-------------|----------|
| .NET 内置 Rate Limiting | 使用 ASP.NET Core 内置的 System.Threading.RateLimiting。支持固定窗口、滑动窗口、令牌桶等策略。框架原生集成，但限流状态默认在内存中（多实例不共享）。 | |
| Redis 自定义实现 | 用 StackExchange.Redis 实现自定义滑动窗口限流。与 Go 实现完全一致，多实例共享限流状态。 | ✓ |
| .NET 框架 + Redis 后端 | 用 .NET 内置 Rate Limiting 的接口，但用 Redis 作为存储后端。兼顾框架便利性和分布式需求。 | |

**User's choice:** Redis 自定义实现（推荐）
**Notes:** 与 Go 实现完全一致，支持多实例部署。

### Q2: 限流算法用哪种？

| Option | Description | Selected |
|--------|-------------|----------|
| 滑动窗口计数 | 按时间窗口限制请求数量（如 100 次/分钟）。Go 的实现方式，简单直观。 | ✓ |
| 固定窗口计数 | 按时间窗口限制请求数量，窗口固定（如每分钟重置）。实现更简单但有边界突发问题。 | |
| 令牌桶算法 | 按令牌桶算法限制，允许一定的突发流量。更平滑但实现复杂度较高。 | |

**User's choice:** 滑动窗口计数（推荐）
**Notes:** 与 Go 实现一致。

### Q3: 限流规则怎么配置？

| Option | Description | Selected |
|--------|-------------|----------|
| 配置文件 | 限流规则（每端点的限制、窗口大小）写在 appsettings.json 中，运行时可修改。 | ✓ |
| 硬编码 | 限流规则硬编码在代码中，简单但修改需要重新部署。 | |

**User's choice:** 配置文件（推荐）
**Notes:** 通过 appsettings.json 配置，运行时可调整。

### Q4: 超出速率限制时返回什么响应？

| Option | Description | Selected |
|--------|-------------|----------|
| 429 + retry_after | 返回 HTTP 429 Too Many Requests，body 包含 retry_after 字段。与 Go 实现一致。 | ✓ |
| 429 + Problem Details | 返回 HTTP 429，body 使用 RFC 7807 Problem Details 格式。更标准化但与 Go 实现有差异。 | |

**User's choice:** 429 + retry_after（推荐）
**Notes:** 与 Go 实现一致。

---

## Claude's Discretion

- CloudFront 签名实现：使用 AWS SDK for .NET（AWSSDK.CloudFront）实现 RSA-SHA1 签名
- CSRF 保护：自定义 HMAC-bound CSRF token 中间件
- 中间件顺序：遵循 Go 的中间件顺序

## Deferred Ideas

None — discussion stayed within phase scope
