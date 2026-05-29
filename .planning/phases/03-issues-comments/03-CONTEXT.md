# Phase 3: Issues & Comments - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Issue and comment domain fully functional — the core of the product. Covers full Issue CRUD, search with filtering, batch operations, parent-child relationships, labels, metadata, reactions, attachments, PR tracking, task management, grouped listing, subscriptions, and Comment CRUD with resolve/unresolve, reactions, and timeline view.

**Requirements covered:** ISSUE-01..12, CMT-01..04

</domain>

<decisions>
## Implementation Decisions

### Issue Search & Filtering
- **D-01:** 使用 EF Core LINQ 动态查询构建器实现搜索和过滤。不使用原生 SQL 或 PostgreSQL 全文搜索。
- **D-02:** 多个过滤条件之间使用 AND 逻辑组合，与 Go 实现一致。
- **D-03:** 全文搜索覆盖 issue 的 title 和 description 字段，使用 ILIKE/Contains 模糊匹配。
- **D-04:** 使用 Cursor-based 分页（基于 created_at 或 id），与 Go 实现一致。
- **D-05:** 支持常用字段排序：created_at、updated_at、priority、status 等。

### Handler Architecture
- **D-06:** 使用单文件 Handler 组织 Issue 端点（IssueEndpoints.cs），用静态扩展方法 MapIssueEndpoints(this WebApplication app) 注册路由。与 Phase 2 的 auth 端点模式一致。
- **D-07:** 使用 ASP.NET Core Authorization Policy 进行 workspace 成员验证和角色检查，与 Phase 2 的 auth 中间件配合。
- **D-08:** 每个端点定义专用的 Response DTO，字段精确匹配 Go 的 JSON 响应格式，确保 100% API 兼容。

### Batch Operations
- **D-09:** 批量更新采用"全部成功或全部回滚"策略，使用数据库事务包裹。
- **D-10:** 批量删除时静默跳过不存在的 ID，只返回实际删除的数量，与 Go 行为一致。
- **D-11:** 批量更新支持常用字段：status、priority、assignee、project_id 等。
- **D-12:** 单次批量操作最多 100 个 issue，防止滥用。

### Comment Rich Text Storage
- **D-13:** 评论内容以 Markdown 原文格式存储，前端 tiptap 编辑器输出的 markdown 原样保存。
- **D-14:** 评论编辑时直接覆盖原内容，不保留历史版本，与 Go 行为一致。
- **D-15:** Markdown 解析在前端完成，C# 后端只存储原始 markdown，不做任何解析处理。
- **D-16:** Comment timeline view 返回纯评论列表，按时间排序。不合并 activity log。

### Claude's Discretion
- Issue 实体配置：为 Issue 实体创建完整的 EF Core 配置，包括索引（workspace_id + created_at 用于分页，title 的 GIN 索引用于搜索）。
- 评论实体配置：Comment 实体需要外键关联到 Issue，支持级联删除。
- 分组查询：Go 支持按 status、priority、assignee、project、label 分组，C# 使用 GROUP BY LINQ 查询实现。
- 附件存储：复用 Phase 1 的存储抽象（S3/Local），通过 Attachment 实体关联到 Issue。
- Issue 依赖关系：IssueDependency 实体已映射，实现前置/后置依赖的 CRUD 和循环依赖检测。

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Go Issue/Comment Handler Implementation
- `server/internal/handler/issue.go` — Issue HTTP handler (3,098 lines), CRUD, search, batch, labels, reactions, attachments, PR tracking
- `server/internal/handler/comment.go` — Comment HTTP handler (1,269 lines), CRUD, resolve/unresolve, reactions, timeline

### Go SQL Queries
- `server/pkg/db/queries/issue.sql` — Issue SQL queries (316 lines), search, filters, CRUD
- `server/pkg/db/queries/comment.sql` — Comment SQL queries (263 lines), CRUD, reactions

### Existing C# Foundation
- `server/src/Multica.Core/Entities/Issue.cs` — Issue entity (1.2K)
- `server/src/Multica.Core/Entities/Comment.cs` — Comment entity (633B)
- `server/src/Multica.Core/Entities/IssueLabel.cs` — Issue-Label relationship
- `server/src/Multica.Core/Entities/IssueReaction.cs` — Issue reactions
- `server/src/Multica.Core/Entities/CommentReaction.cs` — Comment reactions
- `server/src/Multica.Core/Entities/IssueSubscriber.cs` — Issue subscriptions
- `server/src/Multica.Core/Entities/IssueDependency.cs` — Issue dependencies
- `server/src/Multica.Core/Entities/IssuePullRequest.cs` — PR tracking
- `server/src/Multica.Core/Entities/Attachment.cs` — Attachments

### Phase 2 Auth Context
- `.planning/phases/02-auth-middleware/02-CONTEXT.md` — Auth middleware decisions, token validation, workspace role enforcement

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — ISSUE-01..12, CMT-01..04 requirements
- `.planning/ROADMAP.md` — Phase 3 tasks and verification criteria

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **EF Core DbContext** — 49 entities already mapped, including all Issue/Comment related entities
- **Auth middleware pipeline** — JWT/PAT/cookie auth and workspace role enforcement ready (Phase 2)
- **StackExchange.Redis** — Distributed caching ready for issue search result caching
- **Serilog pipeline** — Structured logging ready for request tracing
- **ASP.NET Core Minimal API** — Established pattern with `app.MapGet/MapPost`

### Established Patterns
- **Static extension method registration** — `builder.Services.AddInfrastructure()` pattern from Phase 1
- **Entity configuration** — `IEntityTypeConfiguration<T>` pattern with 12 configs already created
- **Health check endpoints** — `/health`, `/readyz`, `/healthz` already implemented

### Integration Points
- **Program.cs middleware pipeline** — Auth middleware already inserted, issue endpoints go after auth
- **appsettings.json** — Add issue-specific config (search settings, batch limits)
- **EF Core DbContext** — Query issues, comments, labels, reactions for all CRUD operations

</code_context>

<specifics>
## Specific Ideas

- Issue 搜索必须与 Go 的搜索行为完全一致，确保前端无需修改
- 批量操作的请求/响应格式必须精确匹配 Go 的 JSON 结构
- Comment 的 markdown 存储格式必须与 Go 一致，确保前端 tiptap 编辑器正常工作
- Issue 的 polymorphic assignee（member 或 agent）必须正确处理

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 3-Issues & Comments*
*Context gathered: 2026-05-29*
