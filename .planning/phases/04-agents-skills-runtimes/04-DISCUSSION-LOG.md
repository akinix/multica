# Phase 4: Agents, Skills & Runtimes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 04-agents-skills-runtimes
**Areas discussed:** Agent Skills 关联模型, Runtime Liveness 追踪, Runtime Model 缓存策略, Agent Template 目录

---

## Agent Skills 关联模型

| Option | Description | Selected |
|--------|-------------|----------|
| 显式 join entity (推荐) | 使用 AgentSkill 作为显式 join entity，包含 AgentId、SkillId、CreatedAt 等字段。与 Go 的 agent_skills 表结构一致，便于添加额外字段（如排序、优先级）。 | ✓ |
| 隐式 many-to-many | 使用 EF Core 的 Many-to-Many 导航属性，自动生成隐式 join table。代码更简洁，但难以添加额外字段。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 显式 join entity (推荐)
**Notes:** 与 Go 的 agent_skills 中间表结构一致

| Option | Description | Selected |
|--------|-------------|----------|
| 包含 Skill 导航属性 (推荐) | AgentSkill 导航到 Skill，查询时自动加载关联的 Skill 详情（name、description 等）。适合需要显示 skill 详情的场景。 | ✓ |
| 仅存储 SkillId | AgentSkill 只存储 SkillId，查询时需要额外 join。更接近 Go 的 sqlc 模式。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 包含 Skill 导航属性 (推荐)
**Notes:** 查询时自动加载关联的 Skill 详情

| Option | Description | Selected |
|--------|-------------|----------|
| 数据库存储 (推荐) | Skill 文件存储在数据库的 skill_files 表中（文件名 + 内容字段）。与 Go 的 skill_files 表结构一致，适合小型配置文件。 | ✓ |
| 文件系统 + 元数据 | 文件存储在 S3/本地文件系统，数据库只存元数据。适合大型文件，但增加复杂度。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 数据库存储 (推荐)
**Notes:** 与 Go 的 skill_files 表结构一致

| Option | Description | Selected |
|--------|-------------|----------|
| 创建新 Skill (推荐) | 导入时创建新 Skill 记录，保留原 Skill 的所有字段。与 Go 的 skill import 行为一致。 | ✓ |
| Upsert 语义 | 如果同名 Skill 已存在则更新，否则创建。类似 upsert 语义。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 创建新 Skill (推荐)
**Notes:** 与 Go 的 skill import 行为一致

---

## Runtime Liveness 追踪

| Option | Description | Selected |
|--------|-------------|----------|
| Redis + TTL (推荐) | 使用 StackExchange.Redis 存储 runtime liveness 状态，设置 TTL 自动过期。与 Go 的 runtime_liveness_store.go 实现一致。 | ✓ |
| 内存存储 | 使用内存字典 + 定时清理。更简单，但不支持多实例共享。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** Redis + TTL (推荐)
**Notes:** 与 Go 的 runtime_liveness_store.go 实现一致

| Option | Description | Selected |
|--------|-------------|----------|
| 定期心跳 + TTL (推荐) | Runtime 定期发送心跳请求（如每 30 秒），服务端更新 Redis TTL。如果心跳停止，TTL 过期后自动标记为离线。 | ✓ |
| 长 TTL 无心跳 | Runtime 连接时设置较长 TTL（如 5 分钟），不发送心跳。简单但检测延迟高。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 定期心跳 + TTL (推荐)
**Notes:** 与 Go 的 runtime liveness 实现一致

| Option | Description | Selected |
|--------|-------------|----------|
| 独立端点 (推荐) | 提供独立的 GET /api/runtimes/{id}/liveness 端点，返回 runtime 的在线状态和最后心跳时间。 | ✓ |
| 嵌入详情响应 | 在 GET /api/runtimes/{id} 响应中包含 liveness 字段。减少端点数量但增加响应大小。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 独立端点 (推荐)
**Notes:** 与 Go 的独立 liveness 检查端点一致

| Option | Description | Selected |
|--------|-------------|----------|
| 60 秒 TTL / 30 秒心跳 (推荐) | 默认 TTL 60 秒，心跳间隔 30 秒。与 Go 实现一致。 | ✓ |
| 120 秒 TTL / 60 秒心跳 | 默认 TTL 120 秒，心跳间隔 60 秒。更宽松，减少 Redis 操作。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 60 秒 TTL / 30 秒心跳 (推荐)
**Notes:** 与 Go 实现一致

---

## Runtime Model 缓存策略

| Option | Description | Selected |
|--------|-------------|----------|
| Redis 缓存 + TTL (推荐) | Runtime models 缓存在 Redis 中，设置 TTL（如 5 分钟）。与 Go 的 runtime_models_redis_store.go 实现一致。 | ✓ |
| 直接查询 | 直接查询 runtime 获取 models 列表。简单但增加 runtime 负载。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** Redis 缓存 + TTL (推荐)
**Notes:** 与 Go 的 runtime_models_redis_store.go 实现一致

| Option | Description | Selected |
|--------|-------------|----------|
| 主动失效 (推荐) | 当 runtime 上报 models 变更时，主动删除 Redis 缓存。下次查询时重新获取并缓存。 | ✓ |
| 仅 TTL 过期 | 仅依赖 TTL 过期，不主动删除缓存。简单但可能有短暂不一致。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 主动失效 (推荐)
**Notes:** 与 Go 的 runtime models 缓存失效策略一致

| Option | Description | Selected |
|--------|-------------|----------|
| 独立端点 (推荐) | 提供 GET /api/runtimes/{id}/models 端点，返回该 runtime 支持的 models 列表。与 Go 的 runtime_models.go 一致。 | ✓ |
| 嵌入详情响应 | 在 GET /api/runtimes/{id} 响应中包含 models 字段。减少端点数量。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 独立端点 (推荐)
**Notes:** 与 Go 的独立 models 端点一致

| Option | Description | Selected |
|--------|-------------|----------|
| 5 分钟 TTL (推荐) | 默认 TTL 5 分钟。与 Go 实现一致。 | ✓ |
| 10 分钟 TTL | 默认 TTL 10 分钟。减少 Redis 操作，但数据可能更陈旧。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 5 分钟 TTL (推荐)
**Notes:** 与 Go 实现一致

---

## Agent Template 目录

| Option | Description | Selected |
|--------|-------------|----------|
| 数据库存储 (推荐) | Agent templates 存储在数据库的 agent_templates 表中。与 Go 的 agent_templates 表结构一致。 | ✓ |
| 嵌入式 JSON | Templates 作为 JSON 文件嵌入到程序集中。简单但不便于运行时更新。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 数据库存储 (推荐)
**Notes:** 与 Go 的 agent_templates 表结构一致

| Option | Description | Selected |
|--------|-------------|----------|
| 完整模板结构 (推荐) | Template 包含 name、description、system_prompt、model、config 等字段。与 Go 的 agent_templates 表结构一致。 | ✓ |
| 简化模板 | Template 只包含基础字段（name、description），其他配置由用户创建 agent 时手动设置。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 完整模板结构 (推荐)
**Notes:** 与 Go 的 agent_templates 表结构一致

| Option | Description | Selected |
|--------|-------------|----------|
| 可选模板 + 默认值 (推荐) | 创建 agent 时可选择 template，template 的字段作为默认值填充到 agent。用户可修改后再创建。 | ✓ |
| 强制使用模板 | 创建 agent 时必须选择 template，不能跳过。强制使用预设配置。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 可选模板 + 默认值 (推荐)
**Notes:** 与 Go 的 agent template 使用方式一致

| Option | Description | Selected |
|--------|-------------|----------|
| 独立端点 (推荐) | 提供 GET /api/agent-templates 端点，返回可用的 templates 列表。与 Go 的 agent_template.go 一致。 | ✓ |
| 嵌入 agents 响应 | 在 GET /api/agents 响应中包含可用 templates。减少端点数量但增加响应大小。 | |
| Claude's discretion | 让我来决定 | |

**User's choice:** 独立端点 (推荐)
**Notes:** 与 Go 的独立 template catalog 端点一致

---

## Claude's Discretion

- Agent 环境变量管理：使用 agent_env_vars 表存储，与 Go 的 agent_env.go 一致。
- Runtime 更新流程：使用 runtime_update_requests 表记录更新请求和响应，与 Go 的 runtime.go 一致。
- Runtime 本地 Skills：使用 Redis 缓存本地 skills 列表，与 Go 的 runtime_local_skills_redis_store.go 一致。

## Deferred Ideas

None — discussion stayed within phase scope
