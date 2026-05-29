# Phase 4: Agents, Skills & Runtimes - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Agent management, skill system, and runtime management fully functional. Covers Agent CRUD, archive/restore, skills management, environment variables, task listing, template catalog, and Skill CRUD, file management, import, and Runtime CRUD, usage tracking, update request/response flow, model listing, local skills, liveness tracking, archive and delete.

**Requirements covered:** AGT-01..06, SKL-01..03, RT-01..07 (runtimes)

</domain>

<decisions>
## Implementation Decisions

### Agent Skills 关联模型
- **D-01:** 使用显式 join entity (AgentSkill) 建模 Agent 和 Skill 的多对多关系，与 Go 的 agent_skills 中间表结构一致。便于添加额外字段（如排序、优先级）。
- **D-02:** AgentSkill entity 包含导航属性到 Skill entity，查询时自动加载关联的 Skill 详情（name、description 等）。
- **D-03:** Skill 文件（skill_files）存储在数据库的 skill_files 表中（文件名 + 内容字段），与 Go 的 skill_files 表结构一致，适合小型配置文件。
- **D-04:** Skill 导入（SKL-03）创建新 Skill 记录，保留原 Skill 的所有字段，与 Go 的 skill import 行为一致。

### Runtime Liveness 追踪
- **D-05:** 使用 StackExchange.Redis 存储 runtime liveness 状态，设置 TTL 自动过期，与 Go 的 runtime_liveness_store.go 实现一致。
- **D-06:** Runtime 定期发送心跳请求（每 30 秒），服务端更新 Redis TTL（60 秒）。如果心跳停止，TTL 过期后自动标记为离线。
- **D-07:** 提供独立的 GET /api/runtimes/{id}/liveness 端点，返回 runtime 的在线状态和最后心跳时间。
- **D-08:** 默认 TTL 60 秒，心跳间隔 30 秒，与 Go 实现一致。

### Runtime Model 缓存策略
- **D-09:** Runtime models 缓存在 Redis 中，设置 TTL（5 分钟），与 Go 的 runtime_models_redis_store.go 实现一致。
- **D-10:** 当 runtime 上报 models 变更时，主动删除 Redis 缓存。下次查询时重新获取并缓存。
- **D-11:** 提供独立的 GET /api/runtimes/{id}/models 端点，返回该 runtime 支持的 models 列表。
- **D-12:** Models 缓存的默认 TTL 5 分钟，与 Go 实现一致。

### Agent Template 目录
- **D-13:** Agent templates 存储在数据库的 agent_templates 表中，与 Go 的 agent_templates 表结构一致。
- **D-14:** Template 包含完整字段结构：name、description、system_prompt、model、config 等，与 Go 的 agent_templates 表结构一致。
- **D-15:** 创建 agent 时可选择 template，template 的字段作为默认值填充到 agent，用户可修改后再创建。
- **D-16:** 提供独立的 GET /api/agent-templates 端点，返回可用的 templates 列表。

### Claude's Discretion
- Agent 环境变量管理：使用 agent_env_vars 表存储，与 Go 的 agent_env.go 一致。
- Runtime 更新流程：使用 runtime_update_requests 表记录更新请求和响应，与 Go 的 runtime.go 一致。
- Runtime 本地 Skills：使用 Redis 缓存本地 skills 列表，与 Go 的 runtime_local_skills_redis_store.go 一致。

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Go Agent/Skill/Runtime Handler Implementation
- `server/internal/handler/agent.go` — Agent HTTP handler (1,264 lines), CRUD, archive/restore, skills, env vars, tasks
- `server/internal/handler/agent_template.go` — Agent template catalog handler (22K)
- `server/internal/handler/agent_env.go` — Agent environment variables handler (12.1K)
- `server/internal/handler/skill.go` — Skill HTTP handler (1,847 lines), CRUD, file management, import
- `server/internal/handler/runtime.go` — Runtime HTTP handler (891 lines), CRUD, usage, update flow
- `server/internal/handler/runtime_liveness_store.go` — Runtime liveness Redis store (4.9K)
- `server/internal/handler/runtime_models.go` — Runtime models handler (14.7K)
- `server/internal/handler/runtime_models_redis_store.go` — Runtime models Redis store (8.2K)
- `server/internal/handler/runtime_local_skills.go` — Runtime local skills handler (26.1K)
- `server/internal/handler/runtime_local_skills_redis_store.go` — Runtime local skills Redis store (16.6K)

### Go SQL Queries
- `server/pkg/db/queries/agent.sql` — Agent SQL queries (30.4K), CRUD, skills, env vars, tasks

### Existing C# Foundation
- `server/src/Multica.Core/Entities/Agent.cs` — Agent entity (1.1K)
- `server/src/Multica.Core/Entities/AgentRuntime.cs` — Agent-Runtime relationship (839B)
- `server/src/Multica.Core/Entities/AgentSkill.cs` — Agent-Skill join entity (196B)
- `server/src/Multica.Core/Entities/AgentTaskQueue.cs` — Agent task queue (1.2K)
- `server/src/Multica.Core/Entities/Skill.cs` — Skill entity (526B)
- `server/src/Multica.Core/Entities/SkillFile.cs` — Skill file entity (332B)

### Phase 2 & 3 Context
- `.planning/phases/02-auth-middleware/02-CONTEXT.md` — Auth middleware decisions, token validation, workspace role enforcement
- `.planning/phases/03-issues-comments/03-CONTEXT.md` — Issue/Comment handler patterns, EF Core LINQ queries, Response DTOs

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — AGT-01..06, SKL-01..03, RT-01..07 requirements
- `.planning/ROADMAP.md` — Phase 4 tasks and verification criteria

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **EF Core DbContext** — 49 entities already mapped, including all Agent/Skill/Runtime related entities
- **Auth middleware pipeline** — JWT/PAT/cookie auth and workspace role enforcement ready (Phase 2)
- **StackExchange.Redis** — Distributed caching ready for runtime liveness and model caching
- **Serilog pipeline** — Structured logging ready for request tracing
- **ASP.NET Core Minimal API** — Established pattern with `app.MapGet/MapPost`

### Established Patterns
- **Static extension method registration** — `builder.Services.AddInfrastructure()` pattern from Phase 1
- **Entity configuration** — `IEntityTypeConfiguration<T>` pattern with 12 configs already created
- **Single-file Handler organization** — `AgentEndpoints.cs`, `SkillEndpoints.cs`, `RuntimeEndpoints.cs` with `Map*Endpoints(this WebApplication app)` registration
- **Dedicated Response DTOs** — Fields precise match Go's JSON response format

### Integration Points
- **Program.cs middleware pipeline** — Auth middleware already inserted, agent/skill/runtime endpoints go after auth
- **appsettings.json** — Add agent/skill/runtime specific config (liveness TTL, model cache TTL)
- **EF Core DbContext** — Query agents, skills, runtimes, templates for all CRUD operations
- **StackExchange.Redis** — Store runtime liveness state and model cache

</code_context>

<specifics>
## Specific Ideas

- Agent 和 Skill 的多对多关系必须使用显式 join entity，与 Go 的 agent_skills 表结构一致
- Skill 文件必须存储在数据库中，与 Go 的 skill_files 表结构一致
- Runtime liveness 必须使用 Redis + TTL，与 Go 的 runtime_liveness_store.go 实现一致
- Runtime models 必须使用 Redis 缓存 + 主动失效，与 Go 的 runtime_models_redis_store.go 实现一致
- Agent templates 必须存储在数据库中，与 Go 的 agent_templates 表结构一致
- 所有端点的 JSON 响应格式必须与 Go 完全一致，确保前端无需修改

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 4-Agents, Skills & Runtimes*
*Context gathered: 2026-05-29*
