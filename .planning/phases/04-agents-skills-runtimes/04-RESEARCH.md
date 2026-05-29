# Phase 4: Agents, Skills & Runtimes - Research

**Researched:** 2026-05-29
**Domain:** Agent management, Skill system, Runtime management (Go-to-C# migration)
**Confidence:** HIGH

## Summary

Phase 4 is the largest domain phase so far, covering three interrelated subsystems: Agent management (CRUD, archive/restore, skills, env vars, tasks, templates), Skill management (CRUD, file management, import from ClawHub/skills.sh/GitHub), and Runtime management (CRUD, usage tracking, update flow, model listing, local skills, liveness tracking, archive/delete).

The Go implementation totals approximately 4,000+ lines across 10 handler files. Key complexity areas: (1) Skill import logic (~1,100 lines) with multi-source URL detection and parallel fetching, (2) Runtime liveness/models/local-skills using Redis-backed stores with pending-request patterns, (3) Agent template catalog loaded from an in-memory YAML registry, and (4) Agent env vars with audit logging and masked sentinel handling.

The existing C# foundation already has all required entities (Agent, AgentRuntime, AgentSkill, AgentTaskQueue, Skill, SkillFile) and EF Core configurations. Phase 3 established the handler pattern (static class with `Map*Endpoints` extension method, dedicated Response DTOs, EF Core LINQ queries). The missing pieces are: AgentTemplate entity/registry, RuntimeUpdateRequest entity, and three Redis-backed stores (liveness, model list, local skills).

**Primary recommendation:** Follow Phase 3's single-file handler pattern. Split into 3 endpoint files: `AgentEndpoints.cs`, `SkillEndpoints.cs`, `RuntimeEndpoints.cs`. Create dedicated Redis store classes for runtime liveness, model list, and local skills. Use `IHttpClientFactory` for skill import HTTP calls.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** 使用显式 join entity (AgentSkill) 建模 Agent 和 Skill 的多对多关系，与 Go 的 agent_skills 中间表结构一致。便于添加额外字段（如排序、优先级）。
- **D-02:** AgentSkill entity 包含导航属性到 Skill entity，查询时自动加载关联的 Skill 详情（name、description 等）。
- **D-03:** Skill 文件（skill_files）存储在数据库的 skill_files 表中（文件名 + 内容字段），与 Go 的 skill_files 表结构一致，适合小型配置文件。
- **D-04:** Skill 导入（SKL-03）创建新 Skill 记录，保留原 Skill 的所有字段，与 Go 的 skill import 行为一致。
- **D-05:** 使用 StackExchange.Redis 存储 runtime liveness 状态，设置 TTL 自动过期，与 Go 的 runtime_liveness_store.go 实现一致。
- **D-06:** Runtime 定期发送心跳请求（每 30 秒），服务端更新 Redis TTL（60 秒）。如果心跳停止，TTL 过期后自动标记为离线。
- **D-07:** 提供独立的 GET /api/runtimes/{id}/liveness 端点，返回 runtime 的在线状态和最后心跳时间。
- **D-08:** 默认 TTL 60 秒，心跳间隔 30 秒，与 Go 实现一致。
- **D-09:** Runtime models 缓存在 Redis 中，设置 TTL（5 分钟），与 Go 的 runtime_models_redis_store.go 实现一致。
- **D-10:** 当 runtime 上报 models 变更时，主动删除 Redis 缓存。下次查询时重新获取并缓存。
- **D-11:** 提供独立的 GET /api/runtimes/{id}/models 端点，返回该 runtime 支持的 models 列表。
- **D-12:** Models 缓存的默认 TTL 5 分钟，与 Go 实现一致。
- **D-13:** Agent templates 存储在数据库的 agent_templates 表中，与 Go 的 agent_templates 表结构一致。
- **D-14:** Template 包含完整字段结构：name、description、system_prompt、model、config 等，与 Go 的 agent_templates 表结构一致。
- **D-15:** 创建 agent 时可选择 template，template 的字段作为默认值填充到 agent，用户可修改后再创建。
- **D-16:** 提供独立的 GET /api/agent-templates 端点，返回可用的 templates 列表。

### Claude's Discretion

- Agent 环境变量管理：使用 agent_env_vars 表存储，与 Go 的 agent_env.go 一致。
- Runtime 更新流程：使用 runtime_update_requests 表记录更新请求和响应，与 Go 的 runtime.go 一致。
- Runtime 本地 Skills：使用 Redis 缓存本地 skills 列表，与 Go 的 runtime_local_skills_redis_store.go 一致。

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AGT-01 | Agent CRUD (create, read, update, delete) | Go agent.go lines 316-970: ListAgents, GetAgent, CreateAgent, UpdateAgent with full validation |
| AGT-02 | Agent archive/restore | Go agent.go lines 987-1060: ArchiveAgent, RestoreAgent with task cancellation |
| AGT-03 | Agent skills management | Go skill.go lines 1758-1847: ListAgentSkills, SetAgentSkills with transaction |
| AGT-04 | Agent environment variables management | Go agent_env.go: GetAgentEnv, UpdateAgentEnv with audit logging and sentinel masking |
| AGT-05 | Agent task listing | Go agent.go lines 1100-1127: ListAgentTasks with private-agent gate |
| AGT-06 | Agent template catalog | Go agent_template.go: ListAgentTemplates, GetAgentTemplate, CreateAgentFromTemplate |
| SKL-01 | Skill CRUD (create, read, update, delete) | Go skill.go lines 217-453: ListSkills, GetSkill, CreateSkill, UpdateSkill, DeleteSkill |
| SKL-02 | Skill file management | Go skill.go lines 1673-1754: ListSkillFiles, UpsertSkillFile, DeleteSkillFile |
| SKL-03 | Skill import | Go skill.go lines 1590-1669: ImportSkill with ClawHub/skills.sh/GitHub sources |
| RT-01 | Runtime CRUD (create, read, update, delete) | Go runtime.go lines 498-616: ListAgentRuntimes, UpdateAgentRuntime, DeleteAgentRuntime |
| RT-02 | Runtime usage tracking and aggregation | Go runtime.go lines 88-313: GetRuntimeUsage, GetRuntimeUsageByAgent, GetRuntimeUsageByHour |
| RT-03 | Runtime update request/response flow | Go runtime.go: pending-request pattern with Redis store |
| RT-04 | Runtime model listing | Go runtime_models.go: ModelListStore with Redis-backed pending-request pattern |
| RT-05 | Runtime local skills (list, import) | Go runtime_local_skills.go: LocalSkillListStore + LocalSkillImportStore with Redis |
| RT-06 | Runtime liveness tracking | Go runtime_liveness_store.go: RedisLivenessStore with TTL-based heartbeat |
| RT-07 | Runtime archive and delete | Go runtime.go lines 538-891: DeleteAgentRuntime, ArchiveAgentsAndDeleteRuntime cascade |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Agent CRUD | API / Backend | Database / Storage | EF Core LINQ queries against agent table |
| Agent archive/restore | API / Backend | Database / Storage | Transactional updates with task cancellation |
| Agent skills management | API / Backend | Database / Storage | Junction table (agent_skills) with EF Core |
| Agent env vars | API / Backend | Database / Storage | JSONB column with audit logging |
| Agent task listing | API / Backend | Database / Storage | Read-only query against agent_task_queue |
| Agent template catalog | API / Backend | — | In-memory registry loaded from YAML at startup |
| Skill CRUD | API / Backend | Database / Storage | EF Core LINQ queries against skill table |
| Skill file management | API / Backend | Database / Storage | Upsert/delete against skill_files table |
| Skill import | API / Backend | — | HTTP calls to external APIs (ClawHub, GitHub) |
| Runtime CRUD | API / Backend | Database / Storage | EF Core LINQ queries against agent_runtime table |
| Runtime usage tracking | API / Backend | Database / Storage | Aggregation queries against task_usage_hourly |
| Runtime update flow | API / Backend | Database / Storage | Pending-request pattern with Redis |
| Runtime model listing | API / Backend | Database / Storage | Redis-backed pending-request store |
| Runtime local skills | API / Backend | Database / Storage | Redis-backed pending-request store |
| Runtime liveness | API / Backend | Database / Storage | Redis TTL-based heartbeat, DB fallback |
| Runtime archive/delete | API / Backend | Database / Storage | Cascade transaction (archive agents, cancel tasks) |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core Minimal API | .NET 9.0 | HTTP endpoint routing | Already established in Phase 1-3 |
| EF Core + Npgsql | 9.x | ORM + PostgreSQL provider | Already established, 49 entities mapped |
| StackExchange.Redis | 2.x | Redis client for caching | Already established in Phase 1 |
| System.Text.Json | built-in | JSON serialization | Already established, `JsonPropertyName` attributes |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Http | built-in | IHttpClientFactory | Skill import HTTP calls (ClawHub, GitHub API) |
| Microsoft.Extensions.Hosting | built-in | BackgroundService | Runtime liveness sweeper (optional) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| EF Core LINQ | Dapper / raw SQL | LINQ matches Phase 3 pattern; raw SQL only for complex aggregations |
| StackExchange.Redis | IDistributedCache | StackExchange.Redis gives more control (MGET, pipelines, Lua scripts) |
| IHttpClientFactory | new HttpClient() | Factory prevents socket exhaustion; already recommended by ASP.NET Core |

## Package Legitimacy Audit

> No new external packages required for this phase. All dependencies are already established from Phase 1-3.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| StackExchange.Redis | NuGet | 10+ yrs | millions/wk | github.com/StackExchange/StackExchange.Redis | N/A | Already installed |
| Npgsql.EntityFrameworkCore.PostgreSQL | NuGet | 10+ yrs | millions/wk | github.com/npgsql/efcore.pg | N/A | Already installed |
| Microsoft.Extensions.Http | NuGet | 7+ yrs | millions/wk | github.com/dotnet/runtime | N/A | Built-in |

*No new packages to install — all dependencies already present in the project.*

## Architecture Patterns

### System Architecture Diagram

```
HTTP Request
    │
    ▼
┌─────────────────────────────────────┐
│  ASP.NET Core Middleware Pipeline   │
│  (Auth → Workspace → Rate Limit)   │
└─────────────┬───────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│  Endpoint Handlers                  │
│  ┌──────────┐ ┌────────┐ ┌───────┐ │
│  │ Agent    │ │ Skill  │ │Runtime│ │
│  │ Endpoints│ │Endpoints│ │Endpoints│ │
│  └────┬─────┘ └───┬────┘ └───┬───┘ │
└───────┼───────────┼──────────┼──────┘
        │           │          │
        ▼           ▼          ▼
┌─────────────────────────────────────┐
│  EF Core DbContext (MulticaDbContext)│
│  ┌─────────┐ ┌──────┐ ┌─────────┐  │
│  │ Agents  │ │Skills│ │Runtimes │  │
│  │ AgentSkills│ │SkillFiles│ │AgentTaskQueue│  │
│  └─────────┘ └──────┘ └─────────┘  │
└─────────────┬───────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│  PostgreSQL (existing schema)       │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  StackExchange.Redis                │
│  ┌───────────┐ ┌──────────────┐    │
│  │ Liveness  │ │ Model List   │    │
│  │ (TTL keys)│ │ (pending req)│    │
│  └───────────┘ └──────────────┘    │
│  ┌──────────────────────────┐      │
│  │ Local Skills (pending req)│      │
│  └──────────────────────────┘      │
└─────────────────────────────────────┘
```

### Recommended Project Structure

```
server/src/Multica.Api/Handlers/
├── AgentEndpoints.cs          # Agent CRUD, archive/restore, tasks, env vars
├── AgentDtos.cs               # Agent-related Response/Request DTOs
├── AgentTemplateEndpoints.cs  # Agent template catalog (list, get, create-from-template)
├── SkillEndpoints.cs          # Skill CRUD, file management, import
├── SkillDtos.cs               # Skill-related Response/Request DTOs
├── RuntimeEndpoints.cs        # Runtime CRUD, usage, update flow, archive/delete
├── RuntimeDtos.cs             # Runtime-related Response/Request DTOs
├── RuntimeModelEndpoints.cs   # Runtime model listing (pending-request pattern)
├── RuntimeLocalSkillEndpoints.cs  # Runtime local skills (list, import)
└── RuntimeLivenessEndpoints.cs    # Runtime liveness heartbeat/check

server/src/Multica.Infrastructure/Redis/
├── RuntimeLivenessStore.cs    # Redis TTL-based liveness tracking
├── RuntimeModelListStore.cs   # Redis-backed model list request store
└── RuntimeLocalSkillStore.cs  # Redis-backed local skill list/import store

server/src/Multica.Core/Entities/
├── AgentTemplate.cs           # NEW: Agent template entity
└── RuntimeUpdateRequest.cs    # NEW: Runtime update request entity

server/src/Multica.Infrastructure/Data/Configurations/
├── AgentTemplateConfiguration.cs      # NEW
├── RuntimeUpdateRequestConfiguration.cs  # NEW
├── SkillConfiguration.cs              # NEW
├── SkillFileConfiguration.cs          # NEW
└── AgentRuntimeConfiguration.cs       # NEW
```

### Pattern 1: Single-File Handler with Map Extension Method

**What:** Each domain gets a static class with a `Map*Endpoints(this WebApplication app)` extension method that registers all routes.

**When to use:** All agent/skill/runtime endpoint files.

**Example:**
```csharp
// Source: Phase 3 established pattern (IssueHandler.cs, CommentHandler.cs)
public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents");
        group.MapGet("/", ListAgents);
        group.MapPost("/", CreateAgent);
        group.MapGet("/{id}", GetAgent);
        group.MapPatch("/{id}", UpdateAgent);
        group.MapDelete("/{id}", DeleteAgent);
        group.MapPost("/{id}/archive", ArchiveAgent);
        group.MapPost("/{id}/restore", RestoreAgent);
        group.MapGet("/{id}/skills", ListAgentSkills);
        group.MapPut("/{id}/skills", SetAgentSkills);
        group.MapGet("/{id}/env", GetAgentEnv);
        group.MapPut("/{id}/env", UpdateAgentEnv);
        group.MapGet("/{id}/tasks", ListAgentTasks);
    }
}
```

### Pattern 2: Dedicated Response DTOs with JsonPropertyName

**What:** Each endpoint response uses a dedicated record/class with `[JsonPropertyName]` attributes matching Go's JSON field names exactly.

**When to use:** Every response type to ensure 100% API compatibility.

**Example:**
```csharp
// Source: Phase 3 pattern (IssueDtos.cs)
public record AgentResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("workspace_id")] public string WorkspaceId { get; init; } = "";
    [JsonPropertyName("runtime_id")] public string RuntimeId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
    [JsonPropertyName("instructions")] public string Instructions { get; init; } = "";
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("runtime_mode")] public string RuntimeMode { get; init; } = "";
    [JsonPropertyName("runtime_config")] public object? RuntimeConfig { get; init; }
    [JsonPropertyName("custom_args")] public string[] CustomArgs { get; init; } = [];
    [JsonPropertyName("mcp_config")] public JsonElement? McpConfig { get; init; }
    [JsonPropertyName("has_custom_env")] public bool HasCustomEnv { get; init; }
    [JsonPropertyName("custom_env_key_count")] public int CustomEnvKeyCount { get; init; }
    [JsonPropertyName("mcp_config_redacted")] public bool McpConfigRedacted { get; init; }
    [JsonPropertyName("visibility")] public string Visibility { get; init; } = "";
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("max_concurrent_tasks")] public int MaxConcurrentTasks { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("thinking_level")] public string? ThinkingLevel { get; init; }
    [JsonPropertyName("owner_id")] public string? OwnerId { get; init; }
    [JsonPropertyName("skills")] public AgentSkillSummary[] Skills { get; init; } = [];
    [JsonPropertyName("created_at")] public string CreatedAt { get; init; } = "";
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; init; } = "";
    [JsonPropertyName("archived_at")] public string? ArchivedAt { get; init; }
    [JsonPropertyName("archived_by")] public string? ArchivedBy { get; init; }
}
```

### Pattern 3: Workspace Context from Middleware

**What:** Workspace ID is extracted from `HttpContext.Items["WorkspaceId"]` (set by WorkspaceMiddleware), and user ID from `X-User-ID` header.

**When to use:** Every endpoint that needs workspace context.

**Example:**
```csharp
// Source: Phase 3 pattern (IssueHandler.cs, CommentHandler.cs)
var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
if (workspaceId is null)
    return Results.BadRequest(new { error = "workspace_id is required" });

var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
    return Results.Unauthorized();
```

### Pattern 4: Redis-Backed Store Interface

**What:** Define an interface for Redis-backed stores, with a noop fallback when Redis is unavailable.

**When to use:** Runtime liveness, model list, local skills stores.

**Example:**
```csharp
// Source: Go runtime_liveness_store.go pattern
public interface IRuntimeLivenessStore
{
    bool Available { get; }
    Task TouchAsync(string runtimeId, TimeSpan ttl);
    Task<Dictionary<string, bool>> IsAliveBatchAsync(IEnumerable<string> runtimeIds);
    Task ForgetAsync(string runtimeId);
}

public class RedisRuntimeLivenessStore : IRuntimeLivenessStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "mul:runtime:hb:";

    public bool Available => _redis?.IsConnected == true;

    public async Task TouchAsync(string runtimeId, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{runtimeId}", "1", ttl);
    }

    public async Task<Dictionary<string, bool>> IsAliveBatchAsync(IEnumerable<string> runtimeIds)
    {
        var db = _redis.GetDatabase();
        var ids = runtimeIds.ToList();
        var keys = ids.Select(id => (RedisKey)$"{KeyPrefix}{id}").ToArray();
        var values = await db.StringGetAsync(keys);
        return ids.Zip(values, (id, val) => (id, val.HasValue))
                  .ToDictionary(x => x.id, x => x.val);
    }
}
```

### Anti-Patterns to Avoid

- **N+1 queries for agent skills:** Batch-load skills for all agents in ListAgents using a single query, matching Go's `ListAgentSkillsByWorkspace` pattern. Do NOT load skills per-agent in a loop.
- **Exposing custom_env in agent responses:** The Go implementation intentionally removed custom_env from AgentResponse (MUL-2600). Only expose `has_custom_env` and `custom_env_key_count`. Values require the dedicated `GET /api/agents/{id}/env` endpoint.
- **Using `new HttpClient()` for skill imports:** Use `IHttpClientFactory` to prevent socket exhaustion during parallel skill fetches.
- **Missing mcp_config redaction:** Agent actors (requests with X-Agent-ID header) must NEVER see mcp_config, even if the backing member is workspace owner/admin. This prevents lateral movement between agents.
- **Missing thinking_level tri-state:** The Go UpdateAgent handler distinguishes between "field omitted" (no change), "field set to empty string" (clear), and "field set to value" (validate and set). In C#, use `string?` with explicit presence checking via the raw JSON fields.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Redis liveness tracking | Custom Redis key management | StackExchange.Redis `StringSet` with TTL | Simple key-value with TTL is all that's needed |
| HTTP client for imports | `new HttpClient()` | `IHttpClientFactory` via DI | Socket exhaustion prevention, connection pooling |
| JSON serialization | Manual JSON building | `System.Text.Json` with `JsonPropertyName` | Already established, type-safe |
| UUID parsing | Manual string parsing | `Guid.TryParse` / `Guid.Parse` | Built-in, handles all edge cases |
| Workspace/role checks | Custom auth logic | Reuse `HttpContext.Items["WorkspaceId"]` + `X-User-ID` pattern from Phase 3 | Consistent with established middleware pipeline |
| Parallel HTTP fetches | Manual Task.WhenAll | `Task.WhenAll` with `SemaphoreSlim` for concurrency control | Go's `sync.WaitGroup` pattern maps to TPL |

## Runtime State Inventory

> Not applicable — this is a greenfield feature implementation phase, not a rename/refactor/migration phase.

## Common Pitfalls

### Pitfall 1: Agent Skills N+1 Query
**What goes wrong:** Loading skills per-agent in a loop causes N+1 queries.
**Why it happens:** Each agent has a separate DB call for its skills.
**How to avoid:** Batch-load all agent skills for the workspace in one query (`ListAgentSkillsByWorkspace`), then group by agent_id in memory. Match Go's pattern in `ListAgents`.
**Warning signs:** Performance degrades as agent count grows.

### Pitfall 2: Missing mcp_config Redaction for Agent Actors
**What goes wrong:** Agent processes can read other agents' MCP configs (which contain API tokens).
**Why it happens:** Forgetting to check `X-Agent-ID` header and redacting mcp_config.
**How to avoid:** Check `resolveActor` result; if actor is "agent", always redact mcp_config. Apply to ALL agent mutation responses (create, update, archive, restore), not just read endpoints.
**Warning signs:** Security audit flag.

### Pitfall 3: Skill Import Binary File Handling
**What goes wrong:** Binary files (images, fonts) cause PostgreSQL SQLSTATE 22021 errors when stored in TEXT columns.
**Why it happens:** Binary content can't be stored in PostgreSQL TEXT columns.
**How to avoid:** Implement `IsLikelyBinaryFilePath` check (matching Go's extension blacklist) and silently skip binary files during import. Log the skip for debugging.
**Warning signs:** PostgreSQL errors during skill import from GitHub repos with images.

### Pitfall 4: Runtime Liveness Fallback
**What goes wrong:** Redis unavailable causes all runtimes to appear offline.
**Why it happens:** Liveness check fails when Redis is down, no fallback to DB.
**How to avoid:** Implement `Available` property on liveness store. When unavailable, fall back to checking `agent_runtime.last_seen_at` from DB. Match Go's `noopLivenessStore` pattern.
**Warning signs:** All runtimes show offline after Redis restart.

### Pitfall 5: Skill Import File Path Traversal
**What goes wrong:** Malicious skill import can write files outside the intended directory.
**Why it happens:** File paths like `../../etc/passwd` not validated.
**How to avoid:** Implement `ValidateFilePath` check (matching Go's `validateFilePath`): reject absolute paths and paths starting with `..`.
**Warning signs:** Security vulnerability.

### Pitfall 6: Agent Template Skill Deduplication
**What goes wrong:** Creating agent from template duplicates existing workspace skills.
**Why it happens:** No dedup check before importing template skills.
**How to avoid:** Pre-flight check: look up existing skills by `cached_name` in workspace before fetching. Second-chance dedup by actual frontmatter name after fetch. Match Go's two-phase dedup in `CreateAgentFromTemplate`.
**Warning signs:** Duplicate skills appearing in workspace after template-based agent creation.

### Pitfall 7: Runtime Delete Cascade Race Condition
**What goes wrong:** New agent gets bound to runtime between the "check active agents" and "delete runtime" steps.
**Why it happens:** No row-level lock on the runtime during the cascade operation.
**How to avoid:** Use `FOR UPDATE` lock on the runtime row inside the transaction. Compare expected vs actual active agent set. Return 409 with `runtime_delete_plan_changed` if mismatch. Match Go's `ArchiveAgentsAndDeleteRuntime` pattern.
**Warning signs:** 500 errors during runtime deletion under concurrent agent creation.

## Code Examples

Verified patterns from official sources:

### Agent Response DTO Conversion

```csharp
// Source: Go agent.go:agentToResponse pattern, adapted to C#
public static AgentResponse ToResponse(Agent agent)
{
    var customArgs = agent.CustomArgs?.Deserialize<string[]>() ?? [];
    var mcpConfig = agent.McpConfig;
    var envKeyCount = agent.CustomEnv?.Deserialize<Dictionary<string, string>>()?.Count ?? 0;

    return new AgentResponse
    {
        Id = agent.Id.ToString(),
        WorkspaceId = agent.WorkspaceId.ToString(),
        RuntimeId = agent.RuntimeId.ToString(),
        Name = agent.Name,
        Description = agent.Description,
        Instructions = agent.Instructions,
        AvatarUrl = agent.AvatarUrl,
        RuntimeMode = agent.RuntimeMode,
        RuntimeConfig = agent.RuntimeConfig?.Deserialize<object>(),
        CustomArgs = customArgs,
        McpConfig = mcpConfig?.RootElement,
        HasCustomEnv = envKeyCount > 0,
        CustomEnvKeyCount = envKeyCount,
        Visibility = agent.Visibility,
        Status = agent.Status,
        MaxConcurrentTasks = agent.MaxConcurrentTasks,
        Model = agent.Model,
        ThinkingLevel = agent.ThinkingLevel,
        OwnerId = agent.OwnerId?.ToString(),
        Skills = [], // Populated separately
        CreatedAt = agent.CreatedAt.ToString("O"),
        UpdatedAt = agent.UpdatedAt.ToString("O"),
        ArchivedAt = agent.ArchivedAt?.ToString("O"),
        ArchivedBy = agent.ArchivedBy?.ToString(),
    };
}
```

### Redis Liveness Store

```csharp
// Source: Go runtime_liveness_store.go:RedisLivenessStore pattern
public class RedisRuntimeLivenessStore : IRuntimeLivenessStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "mul:runtime:hb:";

    public RedisRuntimeLivenessStore(IConnectionMultiplexer redis) => _redis = redis;

    public bool Available => _redis?.IsConnected == true;

    public async Task TouchAsync(string runtimeId, TimeSpan ttl)
    {
        if (!Available || string.IsNullOrEmpty(runtimeId)) return;
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{runtimeId}", "1", ttl);
    }

    public async Task<Dictionary<string, bool>> IsAliveBatchAsync(IEnumerable<string> runtimeIds)
    {
        if (!Available) return new Dictionary<string, bool>();
        var db = _redis.GetDatabase();
        var ids = runtimeIds.ToList();
        var keys = ids.Select(id => (RedisKey)$"{KeyPrefix}{id}").ToArray();
        var values = await db.StringGetAsync(keys);
        var result = new Dictionary<string, bool>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
            result[ids[i]] = values[i].HasValue;
        return result;
    }

    public async Task ForgetAsync(string runtimeId)
    {
        if (!Available || string.IsNullOrEmpty(runtimeId)) return;
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}{runtimeId}");
    }
}
```

### Skill Import URL Detection

```csharp
// Source: Go skill.go:detectImportSource pattern
public enum ImportSource { ClawHub, SkillsSh, GitHub }

public static (ImportSource source, string normalizedUrl) DetectImportSource(string raw)
{
    raw = raw.Trim();
    if (string.IsNullOrEmpty(raw)) throw new ArgumentException("empty URL");

    var normalized = raw;
    if (!normalized.StartsWith("http://") && !normalized.StartsWith("https://"))
        normalized = "https://" + normalized;

    var uri = new Uri(normalized);
    var host = uri.Host.ToLowerInvariant();

    return host switch
    {
        "skills.sh" or "www.skills.sh" => (ImportSource.SkillsSh, normalized),
        "clawhub.ai" or "www.clawhub.ai" => (ImportSource.ClawHub, normalized),
        "github.com" or "www.github.com" => (ImportSource.GitHub, normalized),
        _ when !raw.Contains("/") || !raw.Contains(".") => (ImportSource.ClawHub, raw),
        _ => throw new ArgumentException($"unsupported source: {host}")
    };
}
```

### Agent Archive with Task Cancellation

```csharp
// Source: Go agent.go:ArchiveAgent pattern
private static async Task<IResult> ArchiveAgent(
    string id,
    HttpContext httpContext,
    MulticaDbContext db,
    ILogger<Program> logger)
{
    var agent = await db.Agents.FindAsync(Guid.Parse(id));
    if (agent is null) return Results.NotFound(new { error = "agent not found" });

    // Permission check
    if (!await CanManageAgent(httpContext, db, agent))
        return Results.Forbid();

    if (agent.ArchivedAt.HasValue)
        return Results.Conflict(new { error = "agent is already archived" });

    var userId = Guid.Parse(httpContext.Request.Headers["X-User-ID"]!);
    agent.ArchivedAt = DateTimeOffset.UtcNow;
    agent.ArchivedBy = userId;
    agent.UpdatedAt = DateTimeOffset.UtcNow;

    // Cancel all pending/active tasks
    var activeTasks = await db.AgentTaskQueues
        .Where(t => t.AgentId == agent.Id &&
                    (t.Status == "queued" || t.Status == "dispatched" || t.Status == "running"))
        .ToListAsync();

    foreach (var task in activeTasks)
    {
        task.Status = "cancelled";
        task.CompletedAt = DateTimeOffset.UtcNow;
    }

    await db.SaveChangesAsync();

    return Results.Ok(AgentToResponse(agent));
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Go sqlc-generated queries | EF Core LINQ queries | Phase 3 | Type-safe, no code generation step |
| Go handler struct methods | C# static extension methods | Phase 3 | Stateless, DI via parameters |
| Go pgx pgtype.UUID | C# Guid | Phase 1 | Simpler, built-in |
| Go json.RawMessage | C# JsonDocument / JsonElement | Phase 1 | Type-safe, lazy parsing |
| Go redis/go-redis | C# StackExchange.Redis | Phase 1 | Same Redis, different client |

**Deprecated/outdated:**
- Go's `agenttmpl.Registry` (in-memory YAML catalog): Replace with database-backed `AgentTemplate` entity per D-13/D-14.
- Go's `runtime_liveness_store.go` noop fallback: C# version uses interface with `Available` property, cleaner DI.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Agent templates should be stored in DB (not YAML files) per D-13 | Architecture | Medium — if YAML preferred, need different entity design |
| A2 | Agent env vars use the same `custom_env` JSONB column on agent table (not a separate `agent_env_vars` table) | Agent Env Vars | Low — Go uses agent.custom_env column directly |
| A3 | Runtime update requests use a separate `runtime_update_requests` table | Runtime Update Flow | Low — Go uses in-memory store, but DB is better for C# |
| A4 | Skill import HTTP timeout is 30 seconds per source | Skill Import | Low — matches Go's `http.Client{Timeout: 30 * time.Second}` |
| A5 | Agent description max length is 255 unicode code points | Agent CRUD | Low — matches Go's `maxAgentDescriptionLength` constant |
| A6 | `LaunchHeader` is a function of `Provider` string, not stored in DB | Runtime Response | Low — Go's `agent.LaunchHeader(rt.Provider)` is a pure function |

## Open Questions

1. **Agent Templates storage approach**
   - What we know: Go uses an in-memory registry loaded from YAML files at startup. CONTEXT.md D-13 says "存储在数据库的 agent_templates 表中".
   - What's unclear: Whether to keep the YAML approach for seeding or go fully DB-backed.
   - Recommendation: Create `AgentTemplate` entity in DB. Seed from YAML on first run via a migration or startup task. This allows runtime template management.

2. **Runtime Update Request flow**
   - What we know: Go uses an in-memory store for pending update requests (similar to model list pattern). Daemon pops pending requests on heartbeat.
   - What's unclear: Whether to use Redis (like model list) or DB for persistence.
   - Recommendation: Use Redis-backed store matching the model list pattern, since the request lifecycle is short-lived (minutes, not days).

3. **Skill import ClawHub/skills.sh availability**
   - What we know: Go imports from ClawHub (clawhub.ai) and skills.sh APIs.
   - What's unclear: Whether these services are still available and their API contracts.
   - Recommendation: Implement all three sources (ClawHub, skills.sh, GitHub). Use `IHttpClientFactory` with proper timeout and error handling. ClawHub/skills.sh failures should not block GitHub imports.

## Environment Availability

> This phase has no new external dependencies beyond what's already established.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| PostgreSQL | All CRUD operations | ✓ | pg17 | — |
| Redis | Runtime liveness, model cache, local skills | ✓ | — | noop fallback for liveness |
| .NET 9.0 SDK | Compilation | ✓ | 9.0.x | — |
| StackExchange.Redis | Redis client | ✓ | 2.x | — |

**Missing dependencies with no fallback:**
- None — all dependencies are available.

**Missing dependencies with fallback:**
- Redis unavailable: Liveness store falls back to DB `last_seen_at` check. Model list and local skills stores return empty/timeout.

## Validation Architecture

> Include this section — `workflow.nyquist_validation` is absent from config, treat as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit + Moq (or NSubstitute) |
| Config file | `server/tests/` directory — see Wave 0 |
| Quick run command | `dotnet test server/tests/` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AGT-01 | Agent CRUD | unit | `dotnet test --filter AgentCrud` | ❌ Wave 0 |
| AGT-02 | Agent archive/restore | unit | `dotnet test --filter AgentArchive` | ❌ Wave 0 |
| AGT-03 | Agent skills management | unit | `dotnet test --filter AgentSkills` | ❌ Wave 0 |
| AGT-04 | Agent env vars | unit | `dotnet test --filter AgentEnv` | ❌ Wave 0 |
| AGT-05 | Agent task listing | unit | `dotnet test --filter AgentTasks` | ❌ Wave 0 |
| AGT-06 | Agent template catalog | unit | `dotnet test --filter AgentTemplate` | ❌ Wave 0 |
| SKL-01 | Skill CRUD | unit | `dotnet test --filter SkillCrud` | ❌ Wave 0 |
| SKL-02 | Skill file management | unit | `dotnet test --filter SkillFiles` | ❌ Wave 0 |
| SKL-03 | Skill import | unit | `dotnet test --filter SkillImport` | ❌ Wave 0 |
| RT-01 | Runtime CRUD | unit | `dotnet test --filter RuntimeCrud` | ❌ Wave 0 |
| RT-02 | Runtime usage | unit | `dotnet test --filter RuntimeUsage` | ❌ Wave 0 |
| RT-03 | Runtime update flow | unit | `dotnet test --filter RuntimeUpdate` | ❌ Wave 0 |
| RT-04 | Runtime models | unit | `dotnet test --filter RuntimeModels` | ❌ Wave 0 |
| RT-05 | Runtime local skills | unit | `dotnet test --filter RuntimeLocalSkills` | ❌ Wave 0 |
| RT-06 | Runtime liveness | unit | `dotnet test --filter RuntimeLiveness` | ❌ Wave 0 |
| RT-07 | Runtime archive/delete | unit | `dotnet test --filter RuntimeDelete` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test server/tests/`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `server/tests/Multica.Api.Tests/AgentEndpointsTests.cs` — covers AGT-01..06
- [ ] `server/tests/Multica.Api.Tests/SkillEndpointsTests.cs` — covers SKL-01..03
- [ ] `server/tests/Multica.Api.Tests/RuntimeEndpointsTests.cs` — covers RT-01..07
- [ ] `server/tests/Multica.Infrastructure.Tests/Redis/` — covers Redis store tests
- [ ] Test project setup: `dotnet new xunit` in `server/tests/`

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Auth middleware (Phase 2) — X-User-ID, X-Agent-ID headers |
| V3 Session Management | no | — |
| V4 Access Control | yes | Workspace membership check, role-based access (owner/admin/member) |
| V5 Input Validation | yes | UUID parsing, description length (255 chars), file path validation |
| V6 Cryptography | no | — |

### Known Threat Patterns for Agent/Skill/Runtime Domain

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Agent env var leakage | Information Disclosure | Dedicated env endpoint with audit logging; never expose in agent response |
| mcp_config lateral movement | Information Disclosure | Agent actors never see mcp_config; redact for non-owner/non-admin members |
| Skill import path traversal | Tampering | ValidateFilePath: reject absolute paths and `..` prefixes |
| Skill import binary injection | Tampering | IsLikelyBinaryFilePath: skip binary extensions (images, fonts, archives) |
| Runtime delete race condition | Denial of Service | FOR UPDATE lock + expected active agent set comparison |
| Agent template skill injection | Tampering | Pre-flight dedup + workspace-scoped skill lookup before import |

## Sources

### Primary (HIGH confidence)
- `server/internal/handler/agent.go` — Full Agent handler implementation (1,264 lines)
- `server/internal/handler/skill.go` — Full Skill handler implementation (1,847 lines)
- `server/internal/handler/runtime.go` — Full Runtime handler implementation (891 lines)
- `server/internal/handler/agent_template.go` — Agent template catalog handler
- `server/internal/handler/agent_env.go` — Agent env vars handler
- `server/internal/handler/runtime_liveness_store.go` — Redis liveness store
- `server/internal/handler/runtime_models.go` — Runtime models handler
- `server/internal/handler/runtime_models_redis_store.go` — Redis model list store
- `server/internal/handler/runtime_local_skills.go` — Runtime local skills handler
- `server/internal/handler/runtime_local_skills_redis_store.go` — Redis local skills store
- `server/pkg/db/queries/agent.sql` — Agent SQL queries
- `server/src/Multica.Core/Entities/*.cs` — All existing C# entities
- `server/src/Multica.Infrastructure/Data/Configurations/*.cs` — EF Core configurations

### Secondary (MEDIUM confidence)
- Phase 3 CONTEXT.md — Established handler patterns, Response DTOs, EF Core LINQ queries
- Phase 3 handler files (IssueHandler.cs, CommentHandler.cs) — C# implementation patterns

### Tertiary (LOW confidence)
- None — all findings are from source code analysis.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all dependencies already established from Phase 1-3
- Architecture: HIGH — Phase 3 patterns directly applicable, Go source fully analyzed
- Pitfalls: HIGH — identified from Go source code comments and edge case handling

**Research date:** 2026-05-29
**Valid until:** 2026-06-28 (30 days — stable domain, no upstream changes expected)
