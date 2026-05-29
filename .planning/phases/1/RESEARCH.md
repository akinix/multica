# Phase 1 Research: Foundation & Data Layer

> Research date: 2026-05-28

## Summary

Phase 1 establishes the ASP.NET Core Minimal API project that will replace the existing Go backend. The Go server (`server/cmd/server/main.go`) uses Chi router, pgx connection pool (pgx/v5), Redis (go-redis/v9), and slog for logging. The database has 139 migrations and 40+ tables with JSONB-heavy columns, polymorphic assignees (member/agent/squad), and UUID primary keys throughout. The sqlc-generated models file (`server/pkg/db/generated/models.go`) defines 40 struct types that map 1:1 to PostgreSQL tables.

The C# project must connect to the same PostgreSQL 17+pgvector database without modifying the schema. EF Core's Database-First (scaffold) approach is the right strategy: use `dotnet ef dbcontext scaffold` to generate entities from the live database, then customize configurations. This avoids hand-writing 40+ entity classes and guarantees column-level fidelity. The existing Go connection pool uses 25 max / 5 min connections with env overrides — the EF Core Npgsql configuration must match or exceed this.

**Primary recommendation:** Scaffold entities from the existing database using EF Core Database-First, then apply Fluent API configurations for JSONB columns, polymorphic relationships, and enum-like text CHECK constraints. Use `NpgsqlDataSourceBuilder` for connection pooling performance parity with pgx.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| HTTP routing & middleware | ASP.NET Core pipeline | — | Framework owns request pipeline |
| Database access (ORM) | EF Core DbContext | Dapper for complex queries | EF Core for CRUD, raw SQL for complex joins |
| Connection pooling | NpgsqlDataSourceBuilder | EF Core internal pool | Npgsql native pooling matches pgx performance |
| Caching / rate limiting | StackExchange.Redis | — | Dedicated Redis client for non-query operations |
| Health checks | ASP.NET Core HealthChecks | — | Framework-integrated health endpoint |
| Structured logging | Serilog | ASP.NET Core ILogger | Serilog sinks + enrichment |
| Configuration | ASP.NET Core IConfiguration | — | Env vars + appsettings.json pattern |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core | 9.0 | Minimal API web framework | Current LTS-adjacent, Minimal API for lightweight endpoints |
| EF Core (Npgsql) | 9.0.x | ORM + PostgreSQL provider | Official .NET ORM, first-class PostgreSQL support |
| Npgsql | 9.0.x | PostgreSQL driver | Official .NET PostgreSQL driver, pgx-equivalent performance |
| StackExchange.Redis | 2.8.x | Redis client | De facto standard, connection multiplexer pattern |
| Serilog.AspNetCore | 8.0.x | Structured logging | JSON sinks, request logging, enrichment |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Serilog.Sinks.Console | 6.0.x | Console JSON output | Always (development + production) |
| Serilog.Sinks.Seq | 8.0.x | Seq integration | Optional — for centralized log aggregation |
| HealthChecks.NpgSql | 8.0.x | PostgreSQL health check | /health and /readyz endpoints |
| HealthChecks.Redis | 8.0.x | Redis health check | /health and /readyz endpoints |
| Microsoft.Extensions.Diagnostics.HealthChecks | 9.0.x | Health check framework | Built-in ASP.NET Core |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| EF Core | Dapper (micro-ORM) | Dapper is faster for raw SQL but loses change tracking, migrations tooling, LINQ. EF Core is better for the 40+ table schema with relationships. Hybrid approach: EF Core primary, Dapper for complex analytics queries. |
| Serilog | Microsoft.Extensions.Logging + Seq/OpenTelemetry | Serilog is more mature for structured logging in .NET. Built-in logging needs additional sinks. |
| StackExchange.Redis | Microsoft.Extensions.Caching.Redis | StackExchange.Redis is lower-level and more flexible (pub/sub, streams, scripting). The Go code uses Redis for caching, rate limiting, realtime relay — need full Redis API. |

## Database Schema Summary

### Tables (40+ total, from models.go)

**Core Domain Tables:**

| Table | Primary Key | Key Columns | Relationships |
|-------|-------------|-------------|---------------|
| `user` | UUID | name, email, avatar_url, language, timezone, onboarded_at, onboarding_questionnaire (JSONB), cloud_waitlist_email | Has many: member, agent (owner), personal_access_token |
| `workspace` | UUID | name, slug (UNIQUE), description, settings (JSONB), context, repos (JSONB), issue_prefix, issue_counter | Has many: member, agent, issue, project, squad |
| `member` | UUID | workspace_id (FK), user_id (FK), role (owner/admin/member) | UNIQUE(workspace_id, user_id) |
| `agent` | UUID | workspace_id (FK), name, runtime_mode (local/cloud), runtime_config (JSONB), visibility, status, max_concurrent_tasks, owner_id (FK user), runtime_id (FK agent_runtime), instructions, custom_env (JSONB), custom_args (JSONB), mcp_config (JSONB), model, thinking_level, archived_at | Belongs to: workspace, user (owner), agent_runtime |
| `agent_runtime` | UUID | workspace_id (FK), daemon_id, name, runtime_mode, provider, status, device_info, metadata (JSONB), last_seen_at, owner_id (FK user), visibility | Has many: agent, agent_task_queue |
| `issue` | UUID | workspace_id (FK), title, description, status (backlog/todo/in_progress/in_review/done/blocked/cancelled), priority (urgent/high/medium/low/none), assignee_type (member/agent/squad), assignee_id, creator_type, creator_id, parent_issue_id (FK self), acceptance_criteria (JSONB), context_refs (JSONB), position (float), due_date, start_date, number (int), project_id (FK), origin_type, origin_id, first_executed_at, metadata (JSONB) | Self-referential parent/child. Polymorphic assignee. |
| `comment` | UUID | issue_id (FK), author_type (member/agent), author_id, content, type (comment/status_change/progress_update/system), parent_id (FK self), workspace_id (FK), resolved_at, resolved_by_type, resolved_by_id | Self-referential threading |
| `agent_task_queue` | UUID | agent_id (FK), issue_id (FK), status (queued/dispatched/running/completed/failed/cancelled), priority, dispatched_at, started_at, completed_at, result (JSONB), error, context (JSONB), runtime_id (FK), session_id, work_dir, trigger_comment_id, chat_session_id, autopilot_run_id, attempt, max_attempts, parent_task_id (FK self), failure_reason, trigger_summary, force_fresh_session, is_leader_task, wait_reason | Polymorphic task queue for agents |
| `squad` | UUID | workspace_id (FK), name, description, leader_id (FK agent), creator_id (FK user), archived_at, archived_by, avatar_url, instructions | Has many: squad_member |
| `squad_member` | UUID | squad_id (FK), member_type (member/agent), member_id, role | Polymorphic membership |
| `project` | UUID | workspace_id (FK), title, description, icon, status, lead_type, lead_id, priority | Has many: issue, project_resource |
| `skill` | UUID | workspace_id (FK), name, description, content, config (JSONB), created_by (FK user) | Has many: skill_file, agent_skill |
| `skill_file` | UUID | skill_id (FK), path, content | Belongs to: skill |
| `chat_session` | UUID | workspace_id (FK), agent_id (FK), creator_id (FK user), title, session_id, work_dir, status, unread_since, runtime_id (FK) | Has many: chat_message |
| `chat_message` | UUID | chat_session_id (FK), role, content, task_id (FK), failure_reason, elapsed_ms | Belongs to: chat_session |
| `autopilot` | UUID | workspace_id (FK), title, description, assignee_id, status, execution_mode, issue_title_template, created_by_type, created_by_id, last_run_at, assignee_type, project_id (FK) | Has many: autopilot_trigger, autopilot_run |
| `autopilot_trigger` | UUID | autopilot_id (FK), kind, enabled, cron_expression, timezone, next_run_at, webhook_token, label, last_fired_at, provider, signing_secret, event_filters (JSONB) | Belongs to: autopilot |
| `autopilot_run` | UUID | autopilot_id (FK), trigger_id, source, status, issue_id (FK), task_id (FK), triggered_at, completed_at, failure_reason, trigger_payload (JSONB), result (JSONB), squad_id (FK) | Belongs to: autopilot |

**Supporting Tables:**

| Table | Purpose | Notable Columns |
|-------|---------|-----------------|
| `issue_label` | Labels for issues | workspace_id, name, color |
| `issue_to_label` | Many-to-many junction | issue_id, label_id (composite PK) |
| `issue_dependency` | Issue blocking relationships | issue_id, depends_on_issue_id, type (blocks/blocked_by/related) |
| `issue_subscriber` | Issue subscription tracking | issue_id, user_type, user_id, reason |
| `issue_reaction` | Emoji reactions on issues | issue_id, workspace_id, actor_type, actor_id, emoji |
| `issue_pull_request` | GitHub PR linkage | issue_id, pull_request_id, linked_by_type, linked_by_id, close_intent |
| `comment_reaction` | Emoji reactions on comments | comment_id, workspace_id, actor_type, actor_id, emoji |
| `inbox_item` | Notification inbox | workspace_id, recipient_type, recipient_id, type, severity, issue_id, title, body, read, archived, actor_type, actor_id, details (JSONB) |
| `activity_log` | Audit trail | workspace_id, issue_id, actor_type, actor_id, action, details (JSONB) |
| `attachment` | File attachments | workspace_id, issue_id, comment_id, uploader_type, uploader_id, filename, url, content_type, size_bytes, chat_session_id, chat_message_id |
| `daemon_connection` | Daemon heartbeat tracking | agent_id, daemon_id, status, last_heartbeat_at, runtime_info (JSONB) |
| `daemon_token` | Daemon auth tokens | token_hash, workspace_id, daemon_id, expires_at |
| `task_token` | Agent task-scoped tokens | token_hash, task_id, agent_id, workspace_id, user_id, expires_at |
| `task_message` | Task execution messages | task_id, seq, type, tool, content, input (JSONB), output |
| `task_usage` | Per-task token usage | task_id, provider, model, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens |
| `task_usage_hourly` | Aggregated hourly usage | bucket_hour, workspace_id, runtime_id, agent_id, project_id, provider, model, input/output/cache tokens, task_count, event_count |
| `task_usage_hourly_dirty` | Rollup queue | bucket_hour, workspace_id, runtime_id, agent_id, project_id, provider, model, enqueued_at |
| `task_usage_hourly_rollup_state` | Rollup watermark | id (int16 PK=1), watermark_at, last_run_started_at, last_run_finished_at, last_run_rows, last_error |
| `personal_access_token` | API tokens | user_id, name, token_hash, token_prefix, expires_at, last_used_at, revoked |
| `verification_code` | Email verification | email, code, expires_at, used, attempts |
| `workspace_invitation` | Invite flow | workspace_id, inviter_id, invitee_email, invitee_user_id, role, status, expires_at |
| `pinned_item` | User pinned items | workspace_id, user_id, item_type, item_id, position |
| `notification_preference` | User notification settings | workspace_id, user_id, preferences (JSONB) |
| `feedback` | User feedback | user_id, workspace_id, message, metadata (JSONB) |
| `contact_sales_inquiry` | Sales form | first_name, last_name, business_email, company_name, etc. |
| `github_installation` | GitHub App installations | workspace_id, installation_id, account_login, account_type |
| `github_pull_request` | Cached PR data | workspace_id, installation_id, repo_owner, repo_name, pr_number, title, state, etc. |
| `github_pull_request_check_suite` | PR check suites | pr_id, suite_id, head_sha, app_id, conclusion, status |
| `webhook_delivery` | Autopilot webhook deliveries | workspace_id, autopilot_id, trigger_id, provider, event, dedupe_key, signature_status, status, raw_body, response_status, response_body |
| `project_resource` | Project links/resources | project_id, workspace_id, resource_type, resource_ref (JSONB), label, position, created_by |
| `agent_skill` | Agent-skill junction | agent_id, skill_id (composite PK) |

### Schema Patterns

1. **UUID primary keys everywhere** — `gen_random_uuid()` default, pgcrypto extension
2. **JSONB columns** for flexible data: `settings`, `runtime_config`, `acceptance_criteria`, `context_refs`, `metadata`, `details`, `result`, `trigger_payload`, `event_filters`, `custom_env`, `custom_args`, `mcp_config`, `preferences`, `repos`, `resource_ref`
3. **Polymorphic actor pattern** — `*_type` (member/agent/system) + `*_id` columns used on: assignee, creator, author, uploader, recipient, actor, linked_by, resolved_by, created_by
4. **CHECK constraints** on status enums: `issue.status`, `issue.priority`, `agent.status`, `agent.runtime_mode`, `agent.visibility`, `member.role`, `comment.type`, etc.
5. **Soft delete** via `archived_at` on agent, squad
6. **Workspace scoping** — most tables have `workspace_id` FK, queries filter by workspace
7. **Self-referential FKs** — `issue.parent_issue_id`, `comment.parent_id`, `agent_task_queue.parent_task_id`
8. **Composite unique constraints** — `member(workspace_id, user_id)`, `issue_to_label(issue_id, label_id)`, `agent_skill(agent_id, skill_id)`
9. **Composite primary keys** — `issue_to_label`, `agent_skill`, `squad_member` use composite PKs

## Existing Query Patterns

### From sqlc Query Files (34 files, 3,625 lines)

**Common patterns to replicate in EF Core:**

1. **COALESCE-based partial updates** — `UpdateAgent`, `UpdateIssue`, `UpdateWorkspace` all use `COALESCE(sqlc.narg('field'), field)` for optional updates. In EF Core, use `Attach` + set only changed properties, or a dedicated patch method.

2. **Workspace-scoped queries** — Nearly every query filters by `workspace_id = $1` as the first parameter. EF Core: use global query filters or always include `.Where(x => x.WorkspaceId == wsId)`.

3. **Polymorphic filtering** — `ListIssues` has complex `involves_user_id` logic joining through agent ownership, squad membership, and squad leadership. This is a CTE-like pattern that may need raw SQL or LINQ with `Union()`.

4. **RETURNING \*** — sqlc uses `RETURNING *` on all INSERT/UPDATE. EF Core handles this natively (tracked entities auto-refresh).

5. **Pagination** — `LIMIT $2 OFFSET $3` pattern. EF Core: `.Skip().Take()`.

6. **CTE recursive queries** — `ListThreadCommentsForIssue` uses `WITH RECURSIVE` for comment threading. Will need raw SQL via `FromSqlRaw`.

7. **JSONB containment** — `@>` operator for metadata filtering. EF Core: use `EF.Property<T>(entity, "PropertyName")` or raw SQL.

8. **Bulk operations** — `ArchiveAgentsByRuntime`, `ArchiveAgentsByIDs` use `= ANY($1::uuid[])`. EF Core: use `.Where(x => ids.Contains(x.Id))`.

9. **Increment atomically** — `IncrementIssueCounter` does `SET issue_counter = issue_counter + 1`. EF Core: use `ExecuteUpdate` with increment expression, or raw SQL.

### Key Query Files to Port

| File | Lines | Complexity | Notes |
|------|-------|------------|-------|
| agent.sql | 30.4K | High | Archive, env management, runtime binding |
| issue.sql | 13.7K | Very High | Complex filtering, polymorphic assignee, involves_user_id |
| comment.sql | 10.7K | High | Recursive CTE for threading |
| runtime.sql | 12.5K | High | Usage aggregation, heartbeat, liveness |
| autopilot.sql | 11.7K | Medium | CRUD + trigger management |
| github.sql | 9.2K | Medium | PR sync, check suites |
| task_usage.sql | 7.2K | High | Hourly rollup, dirty queue |
| chat.sql | 6.3K | Medium | Session + message management |
| squad.sql | 5.4K | Medium | Polymorphic membership |
| webhook_delivery.sql | 4.0K | Medium | Delivery tracking |

## ASP.NET Core Project Structure

### Recommended: Clean Architecture with Vertical Slices

```
server/
├── Multica.sln
├── src/
│   ├── Multica.Api/                    # ASP.NET Core Minimal API host
│   │   ├── Program.cs                  # Entry point, middleware pipeline
│   │   ├── Endpoints/                  # Vertical slice endpoints
│   │   │   ├── Auth/
│   │   │   │   ├── SendCode.cs
│   │   │   │   ├── VerifyCode.cs
│   │   │   │   └── GoogleLogin.cs
│   │   │   ├── Issues/
│   │   │   │   ├── ListIssues.cs
│   │   │   │   ├── CreateIssue.cs
│   │   │   │   ├── UpdateIssue.cs
│   │   │   │   └── DeleteIssue.cs
│   │   │   ├── Agents/
│   │   │   ├── Comments/
│   │   │   ├── Workspaces/
│   │   │   └── ...
│   │   ├── Middleware/                  # Custom middleware
│   │   │   ├── AuthMiddleware.cs
│   │   │   ├── ClientMetadataMiddleware.cs
│   │   │   ├── RequestLoggingMiddleware.cs
│   │   │   └── CspMiddleware.cs
│   │   ├── WebSocketHandlers/          # WebSocket endpoints
│   │   └── HealthChecks/
│   ├── Multica.Core/                   # Domain entities + interfaces
│   │   ├── Entities/                   # EF Core entity classes
│   │   │   ├── User.cs
│   │   │   ├── Workspace.cs
│   │   │   ├── Issue.cs
│   │   │   ├── Agent.cs
│   │   │   └── ...
│   │   ├── Interfaces/                 # Repository/service contracts
│   │   └── Enums/                      # Domain enums (issue status, priority, etc.)
│   └── Multica.Infrastructure/         # Data access + external services
│       ├── Data/
│       │   ├── MulticaDbContext.cs
│       │   ├── Configurations/         # EF Core IEntityTypeConfiguration<T>
│       │   │   ├── IssueConfiguration.cs
│       │   │   ├── AgentConfiguration.cs
│       │   │   └── ...
│       │   └── Migrations/             # EF Core migrations (for future schema changes)
│       ├── Redis/
│       │   └── RedisConnectionProvider.cs
│       └── Services/
│           └── ...
├── tests/
│   ├── Multica.Api.Tests/
│   └── Multica.Infrastructure.Tests/
└── Directory.Build.props               # Shared build settings
```

### Why This Structure

1. **Vertical slices per endpoint** — matches Go's handler-per-file pattern. Each endpoint file contains the route handler, request/response types, and validation.
2. **Separate Core project** — entities and interfaces are framework-agnostic. Infrastructure implements data access.
3. **EF Core Configurations** — `IEntityTypeConfiguration<T>` per entity keeps DbContext clean. Handles JSONB column mapping, CHECK constraints, composite keys.
4. **No Repository pattern** — EF Core DbContext IS the repository. Inject `MulticaDbContext` directly into endpoints. Adding a repository layer over EF Core adds indirection without value.

## EF Core + PostgreSQL Configuration

### Connection Pooling

The Go code uses pgxpool with 25 max / 5 min connections. EF Core with Npgsql must match this:

```csharp
// Program.cs or Infrastructure/DI registration
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder
    .EnableParameterLogging(builder.Environment.IsDevelopment())
    .UseLoggerFactory(loggerFactory);

// Map JSONB columns
dataSourceBuilder.MapEnum<IssueStatus>();  // if using enums for CHECK values

var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContextPool<MulticaDbContext>(options =>
    options
        .UseNpgsql(dataSource, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            npgsqlOptions.CommandTimeout(30);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        })
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)  // read-heavy API
);
```

**Connection string format** (same as Go):
```
Host=localhost;Port=5432;Database=multica;Username=multica;Password=multica;SSL Mode=Disable
```
Or pgx-compatible URI: `postgres://multica:multica@localhost:5432/multica?sslmode=disable`

### Database-First Scaffold Strategy

Since the database already exists with 139 migrations, use reverse engineering:

```bash
dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=multica;Username=multica;Password=multica" \
    Npgsql.EntityFrameworkCore.PostgreSQL \
    --output-dir ../Multica.Core/Entities \
    --context-dir ../Multica.Infrastructure/Data \
    --context MulticaDbContext \
    --data-annotations \
    --force
```

**Post-scaffold customization:**
1. Add `IEntityTypeConfiguration<T>` for each entity in `Multica.Infrastructure/Data/Configurations/`
2. Map JSONB columns to `JsonDocument` or strongly-typed DTOs using `.HasColumnType("jsonb")`
3. Map CHECK constraints to C# enums with `.HasConversion<string>()`
4. Add composite key configurations for junction tables
5. Add global query filters for workspace scoping where appropriate
6. Handle the `"user"` table name (reserved word) with `.ToTable("\"user\"")`

### Handling JSONB Columns

The Go code stores JSONB as `[]byte` (raw bytes). In C#, map to:
- **Read-only JSONB** (settings, metadata): `JsonDocument` or `System.Text.Json.JsonElement`
- **Structured JSONB** (runtime_config, acceptance_criteria): Strongly-typed DTOs with value converter
- **Flexible JSONB** (details, result): `Dictionary<string, object>` or `JsonElement`

```csharp
// Example: strongly-typed JSONB column
entity.Property(e => e.RuntimeConfig)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
        v => JsonSerializer.Deserialize<RuntimeConfigDto>(v, (JsonSerializerOptions)null));
```

### Migration Strategy for Existing Schema

**Do NOT create EF Core migrations for the existing schema.** The database already has 139 migrations managed by the Go tooling. Instead:

1. Scaffold entities from the live database
2. Snapshot the current schema as EF Core's initial migration baseline
3. Use `dotnet ef migrations add InitialBaseline --output-dir Migrations` then delete the `Up()` method body (mark as applied)
4. Future schema changes go through EF Core migrations

### Key Naming Convention Mapping

Go/sqlc uses `snake_case` columns. EF Core defaults to `PascalCase` entities. Configure the naming convention:

```csharp
// In MulticaDbContext.OnModelCreating
foreach (var entity in modelBuilder.Model.GetEntityTypes())
{
    // Map table names to snake_case
    entity.SetTableName(ToSnakeCase(entity.GetTableName()));
    
    // Map column names to snake_case
    foreach (var property in entity.GetProperties())
    {
        property.SetColumnName(ToSnakeCase(property.Name));
    }
}
```

## Redis Configuration

### StackExchange.Redis Setup

The Go code uses 5 separate Redis clients (store, realtime-write, realtime-read, realtime-read-sharded, realtime-read-legacy). For Phase 1, we need the store client:

```csharp
// Infrastructure/Redis/RedisConnectionProvider.cs
public class RedisConnectionProvider : IDisposable
{
    public IConnectionMultiplexer Connection { get; }
    public IDatabase Store { get; }
    
    public RedisConnectionProvider(IConfiguration config)
    {
        var redisUrl = config["REDIS_URL"] ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(redisUrl);
        options.ClientName = "multica-api";
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        
        Connection = ConnectionMultiplexer.Connect(options);
        Store = Connection.GetDatabase();
    }
    
    public void Dispose() => Connection?.Dispose();
}
```

**Registration in DI:**
```csharp
builder.Services.AddSingleton<RedisConnectionProvider>();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    sp.GetRequiredService<RedisConnectionProvider>().Connection);
builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetRequiredService<RedisConnectionProvider>().Store);
```

**Health check:**
```csharp
builder.Services.AddHealthChecks()
    .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), "redis");
```

## Serilog Configuration

```csharp
// Program.cs - before builder.Build()
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Multica")
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// appsettings.json
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Information",
            "Override": {
                "Microsoft.AspNetCore": "Warning",
                "Microsoft.EntityFrameworkCore": "Warning"
            }
        },
        "WriteTo": [
            {
                "Name": "Console",
                "Args": {
                    "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
                }
            }
        ]
    }
}
```

**Request logging middleware:**
```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("ClientPlatform", httpContext.Request.Headers["X-Client-Platform"].ToString());
    };
});
```

## Key Decisions for Planning

| Decision | Options | Recommendation | Rationale |
|----------|---------|----------------|-----------|
| EF Core tracking mode | Tracking vs NoTracking | `NoTracking` as default | Read-heavy API; explicit `Attach` for updates |
| Scaffold vs hand-write entities | dbcontext scaffold vs manual | Scaffold first, then customize | 40+ tables, guarantees column fidelity |
| JSONB mapping strategy | JsonDocument vs string vs strongly-typed | Strongly-typed DTOs where possible, JsonElement for truly flexible | Type safety, but some columns are too dynamic |
| Enum mapping | C# enum vs string | C# enum with `.HasConversion<string>()` | Matches CHECK constraints, type safety |
| Connection pooling | DbContext pooling vs single instance | DbContext pooling (`AddDbContextPool`) | Better perf for high-throughput API |
| Complex queries | LINQ vs raw SQL vs Dapper | LINQ primary, raw SQL for CTEs/complex joins | EF Core LINQ for 90% of queries, `FromSqlRaw` for the rest |
| Global query filters | Yes vs no | No global filters | Workspace context comes from middleware headers, not EF Core |
| Unit of work | Manual SaveChanges vs implicit | Manual per-request SaveChanges | Explicit control over transaction boundaries |

## Risks & Mitigations

### Risk 1: JSONB Column Mapping Fidelity
**What:** The Go code stores/retrieves JSONB as raw `[]byte`. EF Core's JSONB mapping may not preserve exact byte-level fidelity (e.g., key ordering, whitespace).
**Mitigation:** Use `System.Text.Json` with `JsonSerializerOptions { PropertyNamingPolicy = null }` to preserve original key casing. Write comparison tests that verify round-trip fidelity.

### Risk 2: Polymorphic Assignee Pattern
**What:** Issues use `assignee_type` + `assignee_id` (member/agent/squad). EF Core doesn't natively support this — it's not a discriminator column for TPH/TPT.
**Mitigation:** Model as separate nullable FK properties or use a value object. The `involves_user_id` query logic will need raw SQL or complex LINQ unions.

### Risk 3: Schema Drift During Migration
**What:** The Go codebase may add new migrations while the C# migration is in progress.
**Mitigation:** Re-scaffold periodically. The scaffold is a one-time snapshot — entity classes are manually maintained after initial generation.

### Risk 4: Performance Gap
**What:** EF Core generates less optimal SQL than hand-written sqlc queries for complex joins.
**Mitigation:** Use `.AsNoTracking()` everywhere. For hot paths (issue listing, task claiming), use `FromSqlRaw` with the original SQL. Profile early.

### Risk 5: `"user"` Table Name
**What:** PostgreSQL `"user"` is a reserved word. The Go code quotes it (`"user"`). EF Core scaffold may not handle this correctly.
**Mitigation:** Explicit `.ToTable("\"user\"")` in entity configuration.

### Risk 6: Composite Primary Keys
**What:** `issue_to_label`, `agent_skill`, `squad_member` use composite PKs. EF Core supports this but requires explicit configuration.
**Mitigation:** Configure in `IEntityTypeConfiguration<T>` with `.HasKey(e => new { e.IssueId, e.LabelId })`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All C# projects | Check: `dotnet --version` | Need 9.0+ | — |
| PostgreSQL | Database | Check: `pg_isready` | Need 17+ with pgvector | — |
| Redis | Caching/rate limiting | Check: `redis-cli ping` | Any recent version | Skip Redis features (dev only) |
| EF Core tools | Scaffold | Check: `dotnet ef --version` | Need 9.0+ | Install via `dotnet tool install --global dotnet-ef` |

**Missing dependencies with no fallback:**
- .NET SDK 9.0+ — required for all work
- PostgreSQL with existing schema — must have the Multica database running

**Missing dependencies with fallback:**
- Redis — can skip for initial development, use in-memory alternatives

## Validation Architecture

> Phase 1 is infrastructure setup — tests verify connectivity and schema mapping, not business logic.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9+ with EF Core InMemory provider |
| Config file | `tests/Multica.Infrastructure.Tests/Multica.Infrastructure.Tests.csproj` |
| Quick run command | `dotnet test tests/Multica.Infrastructure.Tests/` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FND-01 | API compiles and /health responds | integration | `dotnet test --filter HealthCheck` | Wave 0 |
| FND-02 | EF Core maps all tables | integration | `dotnet test --filter DbContext` | Wave 0 |
| FND-03 | Connection pooling works | integration | `dotnet test --filter ConnectionPool` | Wave 0 |
| FND-04 | LINQ queries return correct data | integration | `dotnet test --filter QueryTests` | Wave 0 |
| FND-06 | Redis connection works | integration | `dotnet test --filter Redis` | Wave 0 |

### Wave 0 Gaps
- [ ] `tests/Multica.Infrastructure.Tests/` project — xunit + EF Core test infrastructure
- [ ] `Multica.Infrastructure.Tests.csproj` — project file with test dependencies
- [ ] `DbContextTests.cs` — verify all entities map correctly
- [ ] `HealthCheckTests.cs` — verify /health endpoint responds

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | ASP.NET Core 9.0 is the target framework | Standard Stack | Need to verify current .NET version available on target |
| A2 | EF Core 9.0 Npgsql provider supports all used PostgreSQL features (JSONB, pgcrypto UUIDs, CHECK constraints) | EF Core Configuration | May need Npgsql-specific workarounds |
| A3 | The database is running and accessible during scaffold | Database-First Strategy | Blocks all entity generation |
| A4 | Go migrations will not add new tables during C# migration | Risks | Schema drift requires re-scaffold |
| A5 | `System.Text.Json` preserves JSONB key ordering from PostgreSQL | JSONB Mapping | May cause subtle comparison failures |
| A6 | StackExchange.Redis 2.8.x is current stable | Redis Configuration | Version may have changed |

## Open Questions

1. **Should we use .NET 8 (LTS) or .NET 9 (STS)?**
   - ✅ RESOLVED: Use .NET 9.0 STS. SDK 9.0.308 available on system. Identical API surface to 8.0 with better PostgreSQL support.

2. **How to handle the `assignee_type` polymorphic pattern in EF Core?**
   - ✅ RESOLVED: Model as separate nullable properties (`AssigneeType`, `AssigneeId`) — not a navigation property or value object. Matches existing DB schema directly. See Task 1.2 IssueConfiguration.

3. **Should complex queries (ListIssues with involves_user_id) be LINQ or raw SQL?**
   - ✅ RESOLVED: Deferred to Phase 3+ (domain-specific phases). Phase 1 only establishes DbSets and entity mappings. Complex queries are implemented alongside their domain endpoints.

4. **What about the Go migration files — do we keep them or generate EF Core migrations?**
   - ✅ RESOLVED: Keep existing Go migrations as-is. Use EF Core migrations only for future schema changes. Mark current schema as baseline via empty Up() method. See Task 1.3.
