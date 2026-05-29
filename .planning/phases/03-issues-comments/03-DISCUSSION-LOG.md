# Phase 3: Issues & Comments - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 3-Issues & Comments
**Areas discussed:** Issue search & filtering, Handler architecture, Batch operations, Comment rich text storage

---

## Issue Search & Filtering

### Search Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| EF Core LINQ 动态查询（推荐） | 用 LINQ 查询构建器动态拼接过滤条件，简单直接。对中小数据量足够，但复杂全文搜索性能可能不如原生 SQL。 | ✓ |
| 混合方案：简单过滤用 LINQ，复杂搜索用原生 SQL | 对搜索和复杂过滤用 Dapper 或 EF Core SqlQuery 执行原生 SQL，性能可控但维护成本更高。 | |
| PostgreSQL 全文搜索 (tsvector) | 为全文搜索引入 Npgsql 的 tsvector/tsquery 支持，利用 PostgreSQL 内置的全文搜索能力。 | |

**User's choice:** EF Core LINQ 动态查询（推荐）

### Filter Combination

| Option | Description | Selected |
|--------|-------------|----------|
| AND 组合（推荐，与 Go 一致） | 所有过滤条件之间用 AND 连接，与 Go 实现一致。 | ✓ |
| 支持 AND/OR 组合 | 支持 AND/OR 两种组合方式，更灵活但实现复杂度更高。 | |

**User's choice:** AND 组合（推荐，与 Go 一致）

### Search Fields

| Option | Description | Selected |
|--------|-------------|----------|
| 标题 + 描述模糊搜索（推荐） | 对 issue 的 title 和 description 做 ILIKE/Contains 模糊搜索，覆盖主要场景。 | ✓ |
| 仅标题搜索 | 只在标题中搜索，实现更简单。 | |
| 标题 + 描述 + 评论 | 标题、描述、评论内容都搜索，覆盖面广但性能开销大。 | |

**User's choice:** 标题 + 描述模糊搜索（推荐）

### Pagination

| Option | Description | Selected |
|--------|-------------|----------|
| Cursor 分页（推荐） | 用 created_at 或 id 做游标，性能稳定，与 Go 实现一致。 | ✓ |
| Offset 分页 | 传统 skip/take 方式，简单但大数据量时性能下降。 | |
| 两者都支持 | 同时支持两种方式，API 参数决定。 | |

**User's choice:** Cursor 分页（推荐）

### Sort Fields

| Option | Description | Selected |
|--------|-------------|----------|
| 常用字段排序（推荐） | 支持 created_at、updated_at、priority、status 等常用字段排序，与 Go 一致。 | ✓ |
| 仅创建时间排序 | 只支持 created_at 排序，简化实现。 | |
| 任意字段排序 | 所有 issue 字段都可排序。 | |

**User's choice:** 常用字段排序（推荐）

---

## Handler Architecture

### Handler Structure

| Option | Description | Selected |
|--------|-------------|----------|
| 单文件 Handler（推荐） | 像 Go 一样把所有 issue 端点放在一个 IssueEndpoints.cs 中，用静态方法注册路由。保持简单，一个文件一个领域。 | ✓ |
| 按子域拆分多个文件 | 按子域拆分：IssueCrudEndpoints.cs、IssueSearchEndpoints.cs、IssueBatchEndpoints.cs、IssueLabelEndpoints.cs 等。 | |
| Handler + Service 层分离 | Handler 只做请求解析和响应，业务逻辑提取到 IssueService 中。 | |

**User's choice:** 单文件 Handler（推荐）

### Route Registration

| Option | Description | Selected |
|--------|-------------|----------|
| 静态扩展方法（推荐） | 用静态扩展方法 MapIssueEndpoints(this WebApplication app)，在 Program.cs 中调用。与 Phase 2 的 auth 端点模式一致。 | ✓ |
| Carter 库 | 用 Carter 库的 ICarterModule 接口组织路由注册。需要引入额外依赖。 | |
| 直接在 Program.cs 注册 | 直接在 Program.cs 中用 app.MapGroup 注册。简单但 Program.cs 会变臃肿。 | |

**User's choice:** 静态扩展方法（推荐）

### Endpoint Authorization

| Option | Description | Selected |
|--------|-------------|----------|
| ASP.NET Core Authorization Policy（推荐） | 在路由定义上用 RequireAuthorization + policy，与 Phase 2 的 auth 中间件配合。 | ✓ |
| 手动检查 | 在每个 handler 方法中手动检查 workspace membership 和 role。 | |
| Endpoint Filter 统一处理 | 自定义 endpoint filter 统一处理 workspace 验证。 | |

**User's choice:** ASP.NET Core Authorization Policy（推荐）

### Response Format

| Option | Description | Selected |
|--------|-------------|----------|
| 专用 Response DTO（推荐） | 每个端点定义专用的 Response DTO，字段精确匹配 Go 的 JSON 响应。最安全但 DTO 数量多。 | ✓ |
| 直接返回 Entity | 直接返回 EF Core Entity，用 System.Text.Json 的属性名映射。简单但可能暴露多余字段。 | |
| 统一包装 + DTO | 定义通用的 ApiResponse<T> 包装，统一成功/失败响应格式。 | |

**User's choice:** 专用 Response DTO（推荐）

---

## Batch Operations

### Batch Update Error Handling

| Option | Description | Selected |
|--------|-------------|----------|
| 全部成功或全部回滚（推荐） | 请求体包含 issue_ids 数组和要更新的字段。所有成功或全部回滚。 | ✓ |
| 逐个更新，部分成功 | 逐个更新，部分失败时返回成功和失败的 ID 列表。 | |
| 事务内部分失败 | 用事务包裹，但允许部分字段更新失败。 | |

**User's choice:** 全部成功或全部回滚（推荐）

### Batch Delete Error Handling

| Option | Description | Selected |
|--------|-------------|----------|
| 静默跳过不存在的 ID（推荐） | 删除不存在的 ID 不报错，只返回实际删除的数量。与 Go 行为一致。 | ✓ |
| 严格模式，任何缺失都报错 | 如果任何 ID 不存在，整个操作回滚并报错。 | |

**User's choice:** 静默跳过不存在的 ID（推荐）

### Batch Update Fields

| Option | Description | Selected |
|--------|-------------|----------|
| 常用字段批量更新（推荐） | 支持 status、priority、assignee、project_id 等常用字段的批量更新。 | ✓ |
| 所有字段批量更新 | 支持所有 issue 字段的批量更新。 | |
| 仅 status + priority | 只支持 status 和 priority 的批量更新。 | |

**User's choice:** 常用字段批量更新（推荐）

### Batch Size Limit

| Option | Description | Selected |
|--------|-------------|----------|
| 最多 100 个（推荐） | 限制单次批量操作最多 100 个 issue，防止滥用。 | ✓ |
| 最多 500 个 | 限制单次批量操作最多 500 个 issue。 | |
| 无限制 | 不限制数量。 | |

**User's choice:** 最多 100 个（推荐）

---

## Comment Rich Text Storage

### Storage Format

| Option | Description | Selected |
|--------|-------------|----------|
| Markdown 原文存储（推荐） | 前端 tiptap 编辑器输出 markdown，后端原样存储 markdown 文本。渲染时前端解析。与 Go 实现一致。 | ✓ |
| JSON 文档结构 | 存储 tiptap 的 JSON 文档结构，支持更丰富的格式但需要前后端同步 schema。 | |
| HTML 存储 | 存储渲染后的 HTML，减少前端解析开销但增加存储体积。 | |

**User's choice:** Markdown 原文存储（推荐）

### Edit History

| Option | Description | Selected |
|--------|-------------|----------|
| 直接覆盖（推荐） | 编辑评论时直接覆盖原内容，简单直接。与 Go 行为一致。 | ✓ |
| 保留编辑历史 | 保留编辑历史，支持查看历史版本。实现复杂度高。 | |

**User's choice:** 直接覆盖（推荐）

### Markdown Parsing

| Option | Description | Selected |
|--------|-------------|----------|
| 前端解析（推荐） | C# 端存储原始 markdown，前端负责解析和渲染。后端不做任何 markdown 处理。 | ✓ |
| 后端解析为 HTML | 后端用 Markdig 库解析 markdown 为 HTML，API 同时返回原始 markdown 和渲染后的 HTML。 | |

**User's choice:** 前端解析（推荐）

### Timeline View

| Option | Description | Selected |
|--------|-------------|----------|
| 纯评论列表（推荐） | 只返回评论列表，按时间排序。简单直接。 | ✓ |
| 评论 + 活动日志合并时间线 | 合并评论和 activity log（状态变更、指派变更等），统一时间线。 | |
| 分端点获取，前端合并 | 分两个端点，评论和活动日志分别获取，前端合并显示。 | |

**User's choice:** 纯评论列表（推荐）

---

## Claude's Discretion

- Issue 实体配置：为 Issue 实体创建完整的 EF Core 配置，包括索引
- 评论实体配置：Comment 实体需要外键关联到 Issue，支持级联删除
- 分组查询：使用 GROUP BY LINQ 查询实现
- 附件存储：复用 Phase 1 的存储抽象
- Issue 依赖关系：实现前置/后置依赖的 CRUD 和循环依赖检测

## Deferred Ideas

None — discussion stayed within phase scope
