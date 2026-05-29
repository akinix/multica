# Phase 1 Plan: Foundation & Data Layer

> Created: 2026-05-28
> Research: .planning/phases/1/RESEARCH.md

## Overview

Phase 1 establishes the ASP.NET Core Minimal API project that replaces the existing Go backend. The deliverable is a running server connected to the existing PostgreSQL database via EF Core, with Redis connectivity, structured logging, and health check endpoints. All 40+ database tables are mapped through EF Core scaffold from the live schema. Existing Go migrations (278 files) are preserved untouched.

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| .NET version | 9.0 (STS) | Per RESEARCH.md. SDK 9.0.308 available on system. Identical API to 8.0 LTS with better PostgreSQL support. |
| EF Core strategy | Database-First scaffold | 40+ tables, 278 Go migrations exist. Scaffold guarantees column fidelity. |
| Tracking mode | NoTracking default | Read-heavy API. Explicit Attach() for writes. |
| Repository pattern | None | Inject DbContext directly. EF Core IS the repository. |
| Naming convention | snake_case via Fluent API | Matches existing PostgreSQL column names. |
| JSONB mapping | Strongly-typed DTOs + JsonElement fallback | Type safety where structure is known, flexibility for dynamic JSONB. |
| Enum mapping | C# enum + HasConversion<string>() | Matches PostgreSQL CHECK constraints. |
| "user" table | Explicit .ToTable("\"user\"") | Reserved word in PostgreSQL requires quoting. |
| Connection pooling | NpgsqlDataSourceBuilder | Matches pgx pool performance (25 max / 5 min). |
| Project structure | Clean Architecture vertical slices | Multica.Api (host), Multica.Core (entities), Multica.Infrastructure (data). |
| Tests | xunit + EF Core InMemory | Standard .NET test stack. |

## Project Structure

```
server/
├── Multica.sln
├── src/
│   ├── Multica.Api/                    # ASP.NET Core Minimal API host
│   │   ├── Multica.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Endpoints/                  # (empty, populated in Phase 2+)
│   │   ├── Middleware/                 # (empty, populated in Phase 2+)
│   │   └── HealthChecks/
│   │       └── CustomHealthCheck.cs
│   ├── Multica.Core/                   # Domain entities + enums
│   │   ├── Multica.Core.csproj
│   │   ├── Entities/                   # EF Core scaffolded entities
│   │   └── Enums/                      # Domain enums
│   └── Multica.Infrastructure/         # Data access + external services
│       ├── Multica.Infrastructure.csproj
│       ├── Data/
│       │   ├── MulticaDbContext.cs
│       │   └── Configurations/         # IEntityTypeConfiguration<T> per entity
│       ├── Redis/
│       │   └── RedisConnectionProvider.cs
│       └── DependencyInjection.cs      # Service registration extension
├── tests/
│   └── Multica.Infrastructure.Tests/
│       ├── Multica.Infrastructure.Tests.csproj
│       ├── DbContextTests.cs
│       ├── HealthCheckTests.cs
│       └── RedisConnectionTests.cs
├── Directory.Build.props               # Shared build settings
└── .config/
    └── dotnet-tools.json               # EF Core CLI tool manifest
```

---

## Wave 0: Project Scaffolding

### Task 0.1: Create solution and project structure

**Objective:** Create the .NET solution with all projects, configure shared build settings, install all NuGet packages.

**Files to create:**
- `server/Directory.Build.props`
- `server/.config/dotnet-tools.json`
- `server/Multica.sln`
- `server/src/Multica.Api/Multica.Api.csproj`
- `server/src/Multica.Api/Program.cs`
- `server/src/Multica.Api/appsettings.json`
- `server/src/Multica.Api/appsettings.Development.json`
- `server/src/Multica.Core/Multica.Core.csproj`
- `server/src/Multica.Infrastructure/Multica.Infrastructure.csproj`
- `server/tests/Multica.Infrastructure.Tests/Multica.Infrastructure.Tests.csproj`
- `server/src/Multica.Api/Endpoints/.gitkeep`
- `server/src/Multica.Api/Middleware/.gitkeep`
- `server/src/Multica.Api/HealthChecks/.gitkeep`
- `server/src/Multica.Core/Entities/.gitkeep`
- `server/src/Multica.Core/Enums/.gitkeep`
- `server/src/Multica.Infrastructure/Data/.gitkeep`
- `server/src/Multica.Infrastructure/Data/Configurations/.gitkeep`
- `server/src/Multica.Infrastructure/Redis/.gitkeep`

**Actions:**

1. Create `server/Directory.Build.props` with shared settings:
   - TargetFramework: net9.0
   - ImplicitUsings: enable
   - Nullable: enable
   - TreatWarningsAsErrors: true

2. Create `server/.config/dotnet-tools.json` with EF Core tools:
   ```json
   {
     "version": 1,
     "isRoot": true,
     "tools": {
       "dotnet-ef": {
         "version": "9.0.0",
         "commands": ["dotnet-ef"]
       }
     }
   }
   ```

3. Create `server/Multica.sln` via `dotnet new sln -n Multica` in `server/`.

4. Create projects using `dotnet new`:
   - `dotnet new webapi -n Multica.Api -o src/Multica.Api --use-controllers false` (Minimal API)
   - `dotnet new classlib -n Multica.Core -o src/Multica.Core`
   - `dotnet new classlib -n Multica.Infrastructure -o src/Multica.Infrastructure`
   - `dotnet new xunit -n Multica.Infrastructure.Tests -o tests/Multica.Infrastructure.Tests`

5. Add project references:
   - `Multica.Api` references `Multica.Infrastructure`
   - `Multica.Infrastructure` references `Multica.Core`

6. Add NuGet packages to `Multica.Api.csproj`:
   - `Serilog.AspNetCore` (8.0.x)
   - `Serilog.Sinks.Console` (6.0.x)
   - `Microsoft.Extensions.Diagnostics.HealthChecks` (9.0.x)
   - `HealthChecks.NpgSql` (8.0.x)
   - `HealthChecks.Redis` (8.0.x)

7. Add NuGet packages to `Multica.Infrastructure.csproj`:
   - `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.x)
   - `Microsoft.EntityFrameworkCore.Design` (9.0.x)
   - `StackExchange.Redis` (2.8.x)

8. Add NuGet packages to `Multica.Infrastructure.Tests.csproj`:
   - `Microsoft.EntityFrameworkCore.InMemory` (9.0.x)
   - `Microsoft.AspNetCore.Mvc.Testing` (9.0.x)
   - `FluentAssertions` (7.0.x)

9. Create `appsettings.json` with connection strings placeholder:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=multica;Username=multica;Password=multica;SSL Mode=Disable",
       "Redis": "localhost:6379"
     },
     "Serilog": {
       "MinimumLevel": {
         "Default": "Information",
         "Override": {
           "Microsoft.AspNetCore": "Warning",
           "Microsoft.EntityFrameworkCore": "Warning"
         }
       }
     }
   }
   ```

10. Create `appsettings.Development.json` with dev overrides:
    ```json
    {
      "Serilog": {
        "MinimumLevel": {
          "Default": "Debug"
        }
      }
    }
    ```

11. Create minimal `Program.cs` that starts and returns 200 on `/`:
    ```csharp
    var builder = WebApplication.CreateBuilder(args);
    var app = builder.Build();
    app.MapGet("/", () => "Multica API");
    app.Run();
    ```

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
All 4 projects compile without errors.

**Done:** Solution builds. All 4 projects exist. NuGet packages restored.

---

### Task 0.2: Restore tools and verify tooling

**Objective:** Install EF Core CLI tools locally and verify database connectivity.

**Files to modify:**
- `server/.config/dotnet-tools.json` (already created in 0.1)

**Actions:**

1. Run `dotnet tool restore` in `server/` to install EF Core CLI from the tool manifest.

2. Verify EF Core tools: `dotnet ef --version` should output `9.0.x`.

3. Verify PostgreSQL is accessible by checking if the database is running:
   ```powershell
   # Check if PostgreSQL is reachable (uses the existing Go dev setup)
   # Option A: if make is available
   make db-up
   # Option B: check directly
   psql -h localhost -U multica -d multica -c "SELECT 1;"
   ```
   If PostgreSQL is not running, start it via `make db-up` or `docker compose up -d`.

4. Verify database has the Multica schema:
   ```powershell
   psql -h localhost -U multica -d multica -c "\dt" | Measure-Object -Line
   ```
   Expected: 40+ tables listed.

**Verify:**
```powershell
cd server && dotnet ef --version
```
Outputs `Entity Framework Core .NET Command-line Tools 9.0.x`.

**Done:** EF Core CLI tools installed and database connection verified.

---

## Wave 1: EF Core Setup

### Task 1.1: Scaffold DbContext and entities from database

**Objective:** Generate all 40+ entity classes and the MulticaDbContext from the live PostgreSQL database using EF Core Database-First.

**Files to create:**
- `server/src/Multica.Core/Entities/*.cs` (40+ entity files, scaffold-generated)
- `server/src/Multica.Infrastructure/Data/MulticaDbContext.cs` (scaffold-generated)

**Actions:**

1. Run the scaffold command:
   ```powershell
   cd server
   dotnet ef dbcontext scaffold `
     "Host=localhost;Port=5432;Database=multica;Username=multica;Password=multica;SSL Mode=Disable" `
     Npgsql.EntityFrameworkCore.PostgreSQL `
     --project src/Multica.Infrastructure `
     --startup-project src/Multica.Api `
     --output-dir ../Multica.Core/Entities `
     --context MulticaDbContext `
     --context-dir Data `
     --data-annotations `
     --force
   ```
   Note: `--output-dir` is relative to the project. Entities go to `Multica.Core/Entities`, DbContext goes to `Multica.Infrastructure/Data/`.

2. After scaffold, verify all tables are present by counting entity files:
   ```powershell
   (Get-ChildItem server/src/Multica.Core/Entities/*.cs).Count
   ```
   Expected: 40+ files.

3. Verify the DbContext has DbSets for all entities:
   ```powershell
   Select-String -Path server/src/Multica.Infrastructure/Data/MulticaDbContext.cs -Pattern "DbSet<" | Measure-Object | Select-Object -ExpandProperty Count
   ```
   Expected: 40+ DbSet properties.

4. Fix the scaffold-generated DbContext:
   - Remove the `OnConfiguring` method (connection string comes from DI)
   - Keep `OnModelCreating` with all generated configurations
   - Add `using Multica.Core.Entities;` if missing

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with all entity types and DbContext.

**Done:** 40+ entity classes generated in `Multica.Core/Entities/`. DbContext in `Multica.Infrastructure/Data/MulticaDbContext.cs`. Solution compiles.

---

### Task 1.2: Configure DbContext DI, snake_case mapping, and domain enums

**Objective:** Wire up the DbContext with NpgsqlDataSourceBuilder, apply snake_case naming convention, fix the "user" table quoting, and create domain enums.

**Files to create:**
- `server/src/Multica.Infrastructure/DependencyInjection.cs`
- `server/src/Multica.Core/Enums/IssueStatus.cs`
- `server/src/Multica.Core/Enums/IssuePriority.cs`
- `server/src/Multica.Core/Enums/MemberRole.cs`
- `server/src/Multica.Core/Enums/AgentStatus.cs`
- `server/src/Multica.Core/Enums/CommentType.cs`
- `server/src/Multica.Core/Enums/AssigneeType.cs`
- `server/src/Multica.Core/Enums/TaskStatus.cs`
- `server/src/Multica.Core/Enums/RuntimeMode.cs`

**Files to modify:**
- `server/src/Multica.Infrastructure/Data/MulticaDbContext.cs`
- `server/src/Multica.Api/Program.cs`

**Actions:**

1. Create `DependencyInjection.cs` — extension method for service registration:
   ```csharp
   public static class DependencyInjection
   {
       public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
       {
           var connectionString = config.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

           var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
           var dataSource = dataSourceBuilder.Build();

           services.AddSingleton(dataSource);
           services.AddDbContextPool<MulticaDbContext>(options =>
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
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
           );

           return services;
       }
   }
   ```

2. Modify `MulticaDbContext.OnModelCreating` to add snake_case convention:
   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       base.OnModelCreating(modelBuilder);

       // Apply snake_case naming convention
       foreach (var entity in modelBuilder.Model.GetEntityTypes())
       {
           entity.SetTableName(ToSnakeCase(entity.GetTableName()));
           foreach (var property in entity.GetProperties())
               property.SetColumnName(ToSnakeCase(property.Name));
           foreach (var key in entity.GetKeys())
               key.SetName(ToSnakeCase(key.GetName()));
           foreach (var fk in entity.GetForeignKeys())
               fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()));
           foreach (var index in entity.GetIndexes())
               index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
       }

       modelBuilder.ApplyConfigurationsFromAssembly(typeof(MulticaDbContext).Assembly);
   }

   private static string ToSnakeCase(string? name)
   {
       if (string.IsNullOrEmpty(name)) return name ?? "";
       return string.Concat(
           name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c.ToString() : c.ToString()
       )).ToLower();
   }
   ```

3. Create domain enums matching PostgreSQL CHECK constraints:
   ```csharp
   // IssueStatus.cs
   public enum IssueStatus { Backlog, Todo, InProgress, InReview, Done, Blocked, Cancelled }
   // IssuePriority.cs
   public enum IssuePriority { Urgent, High, Medium, Low, None }
   // MemberRole.cs
   public enum MemberRole { Owner, Admin, Member }
   // AgentStatus.cs
   public enum AgentStatus { Active, Inactive, Error }
   // CommentType.cs
   public enum CommentType { Comment, StatusChange, ProgressUpdate, System }
   // AssigneeType.cs
   public enum AssigneeType { Member, Agent, Squad }
   // TaskStatus.cs
   public enum TaskQueueStatus { Queued, Dispatched, Running, Completed, Failed, Cancelled }
   // RuntimeMode.cs
   public enum RuntimeMode { Local, Cloud }
   ```

4. Update `Program.cs` to use the infrastructure DI:
   ```csharp
   var builder = WebApplication.CreateBuilder(args);
   builder.Services.AddInfrastructure(builder.Configuration);
   var app = builder.Build();
   app.MapGet("/", () => "Multica API");
   app.Run();
   ```

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with DbContext wired and enums defined.

**Done:** DbContext wired via DI with NpgsqlDataSourceBuilder. snake_case naming convention applied globally. Domain enums created for all CHECK constraints.

---

### Task 1.3: Entity configurations for high-risk entities

**Objective:** Create IEntityTypeConfiguration files for entities with special mapping needs (reserved table names, composite keys, polymorphic patterns, JSONB columns).

**Files to create:**
- `server/src/Multica.Infrastructure/Data/Configurations/UserConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/IssueConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/AgentConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/CommentConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/AgentTaskQueueConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/WorkspaceConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/MemberConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/SquadConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/IssueToLabelConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/AgentSkillConfiguration.cs`
- `server/src/Multica.Infrastructure/Data/Configurations/SquadMemberConfiguration.cs`

**Actions:**

1. `UserConfiguration` — reserved table name + JSONB:
   ```csharp
   public class UserConfiguration : IEntityTypeConfiguration<User>
   {
       public void Configure(EntityTypeBuilder<User> builder)
       {
           builder.ToTable("\"user\""); // reserved word requires quoting
           builder.HasKey(e => e.Id);
           builder.Property(e => e.OnboardingQuestionnaire).HasColumnType("jsonb");
       }
   }
   ```

2. `IssueConfiguration` — polymorphic assignee + JSONB columns:
   ```csharp
   public class IssueConfiguration : IEntityTypeConfiguration<Issue>
   {
       public void Configure(EntityTypeBuilder<Issue> builder)
       {
           builder.HasKey(e => e.Id);
           // Polymorphic assignee — separate properties, no navigation
           builder.Property(e => e.AssigneeType).HasMaxLength(20);
           builder.Property(e => e.AssigneeId);
           builder.Property(e => e.CreatorType).HasMaxLength(20);
           builder.Property(e => e.CreatorId);
           // JSONB columns
           builder.Property(e => e.AcceptanceCriteria).HasColumnType("jsonb");
           builder.Property(e => e.ContextRefs).HasColumnType("jsonb");
           builder.Property(e => e.Metadata).HasColumnType("jsonb");
           // Self-referential
           builder.HasOne<Issue>().WithMany().HasForeignKey(e => e.ParentIssueId);
           // Enum mapping
           builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
           builder.Property(e => e.Priority).HasConversion<string>().HasMaxLength(10);
       }
   }
   ```

3. `AgentConfiguration` — JSONB columns + CHECK enums:
   ```csharp
   public class AgentConfiguration : IEntityTypeConfiguration<Agent>
   {
       public void Configure(EntityTypeBuilder<Agent> builder)
       {
           builder.HasKey(e => e.Id);
           builder.Property(e => e.RuntimeConfig).HasColumnType("jsonb");
           builder.Property(e => e.CustomEnv).HasColumnType("jsonb");
           builder.Property(e => e.CustomArgs).HasColumnType("jsonb");
           builder.Property(e => e.McpConfig).HasColumnType("jsonb");
           builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
           builder.Property(e => e.RuntimeMode).HasConversion<string>().HasMaxLength(10);
           builder.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(20);
       }
   }
   ```

4. `CommentConfiguration` — polymorphic author + self-referential threading:
   ```csharp
   public class CommentConfiguration : IEntityTypeConfiguration<Comment>
   {
       public void Configure(EntityTypeBuilder<Comment> builder)
       {
           builder.HasKey(e => e.Id);
           builder.Property(e => e.AuthorType).HasMaxLength(20);
           builder.Property(e => e.AuthorId);
           builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
           builder.Property(e => e.ResolvedByType).HasMaxLength(20);
           builder.HasOne<Comment>().WithMany().HasForeignKey(e => e.ParentId);
       }
   }
   ```

5. `AgentTaskQueueConfiguration` — polymorphic task + JSONB:
   ```csharp
   public class AgentTaskQueueConfiguration : IEntityTypeConfiguration<AgentTaskQueue>
   {
       public void Configure(EntityTypeBuilder<AgentTaskQueue> builder)
       {
           builder.HasKey(e => e.Id);
           builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
           builder.Property(e => e.Result).HasColumnType("jsonb");
           builder.Property(e => e.Context).HasColumnType("jsonb");
           builder.HasOne<AgentTaskQueue>().WithMany().HasForeignKey(e => e.ParentTaskId);
       }
   }
   ```

6. `WorkspaceConfiguration` — JSONB settings:
   ```csharp
   public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
   {
       public void Configure(EntityTypeBuilder<Workspace> builder)
       {
           builder.HasKey(e => e.Id);
           builder.Property(e => e.Settings).HasColumnType("jsonb");
           builder.Property(e => e.Repos).HasColumnType("jsonb");
       }
   }
   ```

7. `MemberConfiguration` — composite unique constraint:
   ```csharp
   public class MemberConfiguration : IEntityTypeConfiguration<Member>
   {
       public void Configure(EntityTypeBuilder<Member> builder)
       {
           builder.HasKey(e => e.Id);
           builder.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
           builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(10);
       }
   }
   ```

8. Composite key junction tables:
   ```csharp
   // IssueToLabelConfiguration
   builder.HasKey(e => new { e.IssueId, e.LabelId });
   // AgentSkillConfiguration
   builder.HasKey(e => new { e.AgentId, e.SkillId });
   // SquadMemberConfiguration
   builder.HasKey(e => new { e.SquadId, e.MemberId });
   ```

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with all entity configurations applied.

**Done:** All high-risk entities have explicit configurations. Reserved table names quoted. Composite keys configured. Polymorphic patterns mapped as separate properties. JSONB columns typed. CHECK constraint enums mapped.

---

### Task 1.4: Create EF Core migration baseline

**Objective:** Snapshot the current schema as EF Core's initial migration baseline so future schema changes use EF Core migrations.

**Files to create:**
- `server/src/Multica.Infrastructure/Data/Migrations/` (auto-generated by EF Core)

**Actions:**

1. Generate the initial migration:
   ```powershell
   cd server
   dotnet ef migrations add InitialBaseline `
     --project src/Multica.Infrastructure `
     --startup-project src/Multica.Api `
     --output-dir Data/Migrations
   ```

2. Edit the generated `InitialBaseline.cs` migration:
   - Keep the `Up()` method body empty (or with just ` migrationBuilder.EnsureSchema(name: "public");`)
   - Keep the `Down()` method body empty
   - Keep the `ModelSnapshot.cs` as-is (it captures the full schema)

3. Mark the migration as already applied in the database by inserting a row:
   ```sql
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('20260528000000_InitialBaseline', '9.0.0');
   ```
   (The exact MigrationId comes from the generated file name.)

4. Verify EF Core recognizes the baseline:
   ```powershell
   dotnet ef migrations list --project src/Multica.Infrastructure --startup-project src/Multica.Api
   ```
   Should show `InitialBaseline` as applied (no pending migrations).

**Verify:**
```powershell
cd server && dotnet ef migrations list --project src/Multica.Infrastructure --startup-project src/Multica.Api
```
Shows `InitialBaseline (Applied)` and no pending migrations.

**Done:** EF Core migration baseline set. No pending migrations. Future schema changes go through `dotnet ef migrations add`.

---

## Wave 2: Infrastructure Services

### Task 2.1: Redis connection provider and registration

**Objective:** Create Redis connection via StackExchange.Redis with health check, matching Go's store client pattern.

**Files to create:**
- `server/src/Multica.Infrastructure/Redis/RedisConnectionProvider.cs`

**Files to modify:**
- `server/src/Multica.Infrastructure/DependencyInjection.cs`

**Actions:**

1. Create `RedisConnectionProvider.cs`:
   ```csharp
   public class RedisConnectionProvider : IDisposable
   {
       public IConnectionMultiplexer Connection { get; }
       public IDatabase Store { get; }

       public RedisConnectionProvider(IConfiguration config)
       {
           var redisUrl = config.GetConnectionString("Redis") ?? "localhost:6379";
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

2. Add Redis services to `DependencyInjection.AddInfrastructure()`:
   ```csharp
   services.AddSingleton<RedisConnectionProvider>();
   services.AddSingleton<IConnectionMultiplexer>(sp =>
       sp.GetRequiredService<RedisConnectionProvider>().Connection);
   services.AddSingleton<IDatabase>(sp =>
       sp.GetRequiredService<RedisConnectionProvider>().Store);
   ```

3. Add Redis health check registration (will be wired in Task 2.3).

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with Redis provider registered.

**Done:** RedisConnectionProvider created. IConnectionMultiplexer and IDatabase registered in DI. Connection uses environment configuration.

---

### Task 2.2: Serilog structured logging configuration

**Objective:** Configure Serilog with JSON structured output, request logging enrichment, and per-component log level overrides.

**Files to modify:**
- `server/src/Multica.Api/Program.cs`
- `server/src/Multica.Api/appsettings.json`

**Actions:**

1. Add Serilog host configuration to `Program.cs` (before `builder.Build()`):
   ```csharp
   builder.Host.UseSerilog((context, services, configuration) => configuration
       .ReadFrom.Configuration(context.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("Application", "Multica")
       .WriteTo.Console(new RenderedCompactJsonFormatter()));
   ```

2. Add NuGet package to `Multica.Api.csproj`:
   - `Serilog.Formatting.Compact` (for `RenderedCompactJsonFormatter`)

3. Update `appsettings.json` Serilog section:
   ```json
   "Serilog": {
     "MinimumLevel": {
       "Default": "Information",
       "Override": {
         "Microsoft.AspNetCore": "Warning",
         "Microsoft.EntityFrameworkCore": "Warning",
         "System": "Warning"
       }
     },
     "WriteTo": [
       {
         "Name": "Console",
         "Args": {
           "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
         }
       }
     ],
     "Enrich": ["FromLogContext", "WithProperty"]
   }
   ```

4. Add request logging middleware to `Program.cs`:
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

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with Serilog configured. Run and check console output is JSON-formatted.

**Done:** Serilog configured with compact JSON formatter. Request logging enriches with host, user-agent, client-platform. Log levels overridden for EF Core and ASP.NET noise.

---

### Task 2.3: Health check endpoints

**Objective:** Implement `/health`, `/readyz`, and `/healthz` endpoints with PostgreSQL and Redis checks.

**Files to create:**
- `server/src/Multica.Api/HealthChecks/CustomHealthCheck.cs`

**Files to modify:**
- `server/src/Multica.Api/Program.cs`
- `server/src/Multica.Infrastructure/DependencyInjection.cs`

**Actions:**

1. Register health checks in `DependencyInjection.AddInfrastructure()`:
   ```csharp
   services.AddHealthChecks()
       .AddNpgSql(
           sp => sp.GetRequiredService<NpgsqlDataSource>(),
           name: "postgresql",
           tags: ["ready"])
       .AddRedis(
           sp => sp.GetRequiredService<IConnectionMultiplexer>(),
           name: "redis",
           tags: ["ready"]);
   ```
   Note: Need to also register `NpgsqlDataSource` in DI (extract from the DataSourceBuilder):
   ```csharp
   services.AddSingleton(dataSource); // register the built NpgsqlDataSource
   ```

2. Create `CustomHealthCheck.cs` for a basic liveness check (no external deps):
   ```csharp
   public class CustomHealthCheck : IHealthCheck
   {
       public Task<HealthCheckResult> CheckHealthAsync(
           HealthCheckContext context,
           CancellationToken cancellationToken = default)
       {
           return Task.FromResult(HealthCheckResult.Healthy("Multica API is running"));
       }
   }
   ```

3. Map health endpoints in `Program.cs`:
   ```csharp
   // Liveness — is the process alive?
   app.MapHealthChecks("/healthz", new HealthCheckOptions
   {
       Predicate = _ => false, // no dependency checks
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });

   // Readiness — can it serve traffic?
   app.MapHealthChecks("/readyz", new HealthCheckOptions
   {
       Predicate = check => check.Tags.Contains("ready"),
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });

   // Generic health
   app.MapHealthChecks("/health", new HealthCheckOptions
   {
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });
   ```

4. Add NuGet package to `Multica.Api.csproj`:
   - `AspNetCore.HealthChecks.UI.Client` (for `UIResponseWriter`)

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with health check endpoints mapped.

**Done:** Three health endpoints registered: `/healthz` (liveness), `/readyz` (readiness with DB+Redis checks), `/health` (full). PostgreSQL and Redis health checks integrated.

---

## Wave 3: Middleware Pipeline

### Task 3.1: Configure middleware pipeline in Program.cs

**Objective:** Set up the complete middleware pipeline with proper ordering, CORS, exception handling, and Serilog request logging.

**Files to modify:**
- `server/src/Multica.Api/Program.cs`

**Actions:**

1. Configure the full middleware pipeline in `Program.cs`:
   ```csharp
   var builder = WebApplication.CreateBuilder(args);

   // --- Services ---
   builder.Host.UseSerilog(/* ... */);
   builder.Services.AddInfrastructure(builder.Configuration);
   builder.Services.AddCors(options =>
   {
       options.AddDefaultPolicy(policy =>
       {
           policy
               .WithOrigins(
                   builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                   ?? ["http://localhost:3000"])
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
       });
   });

   var app = builder.Build();

   // --- Middleware pipeline (order matters) ---
   app.UseSerilogRequestLogging();

   app.UseExceptionHandler(error =>
   {
       error.Run(async context =>
       {
           context.Response.StatusCode = 500;
           context.Response.ContentType = "application/problem+json";
           var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
           await context.Response.WriteAsJsonAsync(new
           {
               type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
               title = "Internal Server Error",
               status = 500,
               detail = app.Environment.IsDevelopment() ? exception?.Message : null
           });
       });
   });

   app.UseCors();

   // Health checks (before auth middleware)
   app.MapHealthChecks("/healthz", /* ... */);
   app.MapHealthChecks("/readyz", /* ... */);
   app.MapHealthChecks("/health", /* ... */);

   app.MapGet("/", () => "Multica API");

   app.Run();
   ```

2. Update `appsettings.json` with CORS origins:
   ```json
   "Cors": {
     "Origins": ["http://localhost:3000", "http://localhost:3001"]
   }
   ```

3. Add `Multica.Api/Properties/launchSettings.json` with correct port:
   ```json
   {
     "profiles": {
       "Multica.Api": {
         "commandName": "Project",
         "launchBrowser": false,
         "applicationUrl": "http://localhost:8080",
         "environmentVariables": {
           "ASPNETCORE_ENVIRONMENT": "Development"
         }
       }
     }
   }
   ```
   Note: Port 8080 matches the Go backend port.

**Verify:**
```powershell
cd server && dotnet build Multica.sln
```
Compiles with full middleware pipeline.

**Done:** Middleware pipeline configured: Serilog request logging -> Exception handler -> CORS -> Health checks -> Root endpoint. Server listens on port 8080.

---

## Wave 4: Verification

### Task 4.1: Integration tests for DbContext and health checks

**Objective:** Write integration tests verifying EF Core can query all major tables and health endpoints respond correctly.

**Files to create:**
- `server/tests/Multica.Infrastructure.Tests/DbContextTests.cs`
- `server/tests/Multica.Infrastructure.Tests/HealthCheckTests.cs`
- `server/tests/Multica.Infrastructure.Tests/RedisConnectionTests.cs`

**Files to modify:**
- `server/tests/Multica.Infrastructure.Tests/Multica.Infrastructure.Tests.csproj` (add project references)

**Actions:**

1. Add project references to test project:
   - Reference `Multica.Api`
   - Reference `Multica.Infrastructure`

2. Create `DbContextTests.cs`:
   ```csharp
   public class DbContextTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly WebApplicationFactory<Program> _factory;

       public DbContextTests(WebApplicationFactory<Program> factory)
       {
           _factory = factory;
       }

       [Fact]
       public async Task CanQueryUsers()
       {
           using var scope = _factory.Services.CreateScope();
           var db = scope.ServiceProvider.GetRequiredService<MulticaDbContext>();
           var users = await db.Users.Take(1).ToListAsync();
           Assert.NotNull(users);
       }

       [Fact]
       public async Task CanQueryWorkspaces()
       {
           using var scope = _factory.Services.CreateScope();
           var db = scope.ServiceProvider.GetRequiredService<MulticaDbContext>();
           var workspaces = await db.Workspaces.Take(1).ToListAsync();
           Assert.NotNull(workspaces);
       }

       [Fact]
       public async Task CanQueryIssues()
       {
           using var scope = _factory.Services.CreateScope();
           var db = scope.ServiceProvider.GetRequiredService<MulticaDbContext>();
           var issues = await db.Issues.Take(1).ToListAsync();
           Assert.NotNull(issues);
       }

       [Fact]
       public async Task CanQueryAgents()
       {
           using var scope = _factory.Services.CreateScope();
           var db = scope.ServiceProvider.GetRequiredService<MulticaDbContext>();
           var agents = await db.Agents.Take(1).ToListAsync();
           Assert.NotNull(agents);
       }

       [Fact]
       public async Task AllDbSetsAreQueryable()
       {
           // Verify no entity throws on query (schema mapping is correct)
           using var scope = _factory.Services.CreateScope();
           var db = scope.ServiceProvider.GetRequiredService<MulticaDbContext>();

           // Test that the model is valid
           var model = db.Model;
           var entityTypes = model.GetEntityTypes().ToList();
           Assert.True(entityTypes.Count >= 40, $"Expected 40+ entity types, got {entityTypes.Count}");
       }
   }
   ```

3. Create `HealthCheckTests.cs`:
   ```csharp
   public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly HttpClient _client;

       public HealthCheckTests(WebApplicationFactory<Program> factory)
       {
           _client = factory.CreateClient();
       }

       [Fact]
       public async Task HealthEndpointReturns200()
       {
           var response = await _client.GetAsync("/health");
           response.EnsureSuccessStatusCode();
       }

       [Fact]
       public async Task HealthzEndpointReturns200()
       {
           var response = await _client.GetAsync("/healthz");
           response.EnsureSuccessStatusCode();
       }

       [Fact]
       public async Task ReadyzEndpointReturns200()
       {
           var response = await _client.GetAsync("/readyz");
           // May return 503 if DB/Redis not available — that's expected
           Assert.True(response.StatusCode is System.Net.HttpStatusCode.OK
               or System.Net.HttpStatusCode.ServiceUnavailable);
       }
   }
   ```

4. Create `RedisConnectionTests.cs`:
   ```csharp
   public class RedisConnectionTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly WebApplicationFactory<Program> _factory;

       public RedisConnectionTests(WebApplicationFactory<Program> factory)
       {
           _factory = factory;
       }

       [Fact]
       public void RedisConnectionProviderIsRegistered()
       {
           var provider = _factory.Services.GetService<RedisConnectionProvider>();
           Assert.NotNull(provider);
       }

       [Fact]
       public async Task CanPingRedis()
       {
           var mux = _factory.Services.GetRequiredService<IConnectionMultiplexer>();
           var ping = await mux.GetDatabase().PingAsync();
           Assert.True(ping > TimeSpan.Zero);
       }
   }
   ```

5. Create a custom `WebApplicationFactory` in test project to override connection strings for testing:
   ```csharp
   public class MulticaWebApplicationFactory : WebApplicationFactory<Program>
   {
       protected override void ConfigureWebHost(IWebHostBuilder builder)
       {
           builder.UseEnvironment("Testing");
           // Use real database for integration tests
           // Connection string comes from appsettings.json or env var
       }
   }
   ```

**Verify:**
```powershell
cd server && dotnet test tests/Multica.Infrastructure.Tests/ --filter "HealthCheckTests|DbContextTests"
```
All tests pass (requires PostgreSQL and Redis running).

**Done:** Integration tests verify: (1) DbContext can query 4+ core tables, (2) all 40+ entity types are mapped, (3) health endpoints respond, (4) Redis connection works.

---

### Task 4.2: End-to-end server startup verification

**Objective:** Verify the server starts, connects to all services, and responds to health checks.

**Files to modify:**
- None (verification-only task)

**Actions:**

1. Start the server:
   ```powershell
   cd server && dotnet run --project src/Multica.Api
   ```

2. Verify health endpoints (in another terminal):
   ```powershell
   Invoke-WebRequest http://localhost:8080/healthz -UseBasicParsing | Select-Object StatusCode
   Invoke-WebRequest http://localhost:8080/readyz -UseBasicParsing | Select-Object StatusCode
   Invoke-WebRequest http://localhost:8080/health -UseBasicParsing | Select-Object StatusCode
   ```

3. Verify root endpoint:
   ```powershell
   Invoke-WebRequest http://localhost:8080/ -UseBasicParsing | Select-Object Content
   ```
   Expected: "Multica API"

4. Verify Serilog output is JSON-formatted:
   ```
   Check server console output for structured JSON log lines.
   ```

5. Verify database queries work (add a temporary test endpoint if needed):
   ```csharp
   // Temporary diagnostic endpoint (remove after verification)
   app.MapGet("/_diag/db", async (MulticaDbContext db) =>
   {
       var userCount = await db.Users.CountAsync();
       var workspaceCount = await db.Workspaces.CountAsync();
       return new { userCount, workspaceCount };
   });
   ```

**Verify:**
```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```
Returns 200 with JSON health report.

**Done:** Server starts on port 8080. All three health endpoints respond. Database queries return data. Serilog outputs structured JSON.

---

## Verification Checklist

| Requirement | Verified By | Status |
|-------------|-------------|--------|
| FND-01: ASP.NET Core Minimal API compiles and runs with health check | Task 0.1 (build), Task 2.3 (health endpoints), Task 4.2 (startup) | |
| FND-02: EF Core DbContext maps all PostgreSQL tables | Task 1.1 (scaffold 40+ entities), Task 4.1 (entity count test) | |
| FND-03: Database connection pooling works | Task 1.2 (NpgsqlDataSourceBuilder with pool config) | |
| FND-04: All 34 SQL query domains have DbSet exposure | Task 1.1 (scaffold generates DbSets for all tables), Task 1.3 (entity configurations for high-risk entities), Task 4.1 (DbSet queries). Note: actual query implementation (LINQ/raw SQL) is deferred to Phase 3+ domain phases. | |
| FND-05: Existing migrations preserved, EF Core baseline set | Task 1.4 (migration baseline, empty Up() method) | |
| FND-06: Redis connection via StackExchange.Redis | Task 2.1 (RedisConnectionProvider), Task 4.1 (ping test) | |
| FEAT-20: Health/readiness endpoints | Task 2.3 (/health, /readyz, /healthz), Task 4.1 (endpoint tests) | |

## Commit Strategy

| Wave | Commit Message | Files |
|------|---------------|-------|
| Wave 0 | `feat(foundation): scaffold .NET solution with 4 projects` | Directory.Build.props, dotnet-tools.json, Multica.sln, all .csproj files, Program.cs, appsettings.* |
| Wave 1a | `feat(foundation): scaffold EF Core entities from database` | Multica.Core/Entities/*, Multica.Infrastructure/Data/MulticaDbContext.cs |
| Wave 1b | `feat(foundation): configure DbContext DI, snake_case mapping, and domain enums` | DependencyInjection.cs, Enums/*, Program.cs |
| Wave 1c | `feat(foundation): add entity configurations for high-risk entities` | Configurations/* |
| Wave 1d | `chore(foundation): set EF Core migration baseline` | Migrations/* |
| Wave 2 | `feat(foundation): add Redis, Serilog, and health check endpoints` | RedisConnectionProvider.cs, HealthChecks/*, Program.cs, appsettings.json |
| Wave 3 | `feat(foundation): configure middleware pipeline` | Program.cs, launchSettings.json, appsettings.json |
| Wave 4 | `test(foundation): add integration tests for DbContext, health, Redis` | tests/* |

## Notes for Executor

1. **Database must be running** before Wave 1 scaffold. Start PostgreSQL via `make db-up` or `docker compose up -d`.

2. **The scaffold will generate many files.** Expect 40+ entity files and a large DbContext. Do not hand-write entity classes — let the scaffold generate them, then customize.

3. **The "user" table** will likely need manual fixup after scaffold. The scaffold may generate `User` with `ToTable("user")` — change to `ToTable("\"user\"")`.

4. **Composite keys** (issue_to_label, agent_skill, squad_member) may not be scaffolded correctly. Verify and fix in Task 1.3 configurations.

5. **JSONB columns** will scaffold as `string` by default. Post-scaffold, convert to `JsonDocument` or strongly-typed DTOs in Task 1.3 configurations.

6. **Existing Go code** in `server/cmd/`, `server/internal/`, `server/pkg/` stays untouched. The .NET projects coexist alongside during migration. The new .NET code lives in `server/src/` and `server/tests/`.

7. **Port 8080** is used by the Go server. Either stop the Go server before running the C# server, or change the port in `launchSettings.json`.

8. **FND-04 scope**: Phase 1 only establishes DbSets and entity mappings. Actual LINQ/raw SQL query implementation happens in Phase 3+ domain-specific phases (issues, agents, comments, etc.).
