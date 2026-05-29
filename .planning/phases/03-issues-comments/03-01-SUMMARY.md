---
phase: 03-issues-comments
plan: 01
subsystem: api
tags: [issues, crud, ef-core, minimal-api, entity-configuration]

# Dependency graph
requires:
  - phase: 02-auth-middleware
    provides: [auth-middleware, workspace-middleware, jwt-validation]
provides:
  - Issue CRUD endpoints (POST, GET, PATCH, DELETE)
  - Issue entity with EF Core configuration
  - MulticaDbContext with all entity sets
  - IssueResponse DTO matching Go JSON format
affects: [03-02-comment-crud, 03-03-issue-search, 03-04-batch-operations]

# Tech tracking
tech-stack:
  added: []
  patterns: [static-extension-method-registration, entity-configuration, dto-mapping]

key-files:
  created:
    - server/src/Multica.Api/Handlers/IssueHandler.cs
    - server/src/Multica.Core/Entities/Issue.cs
    - server/src/Multica.Core/Entities/User.cs
    - server/src/Multica.Core/Entities/Workspace.cs
    - server/src/Multica.Core/Entities/Member.cs
    - server/src/Multica.Core/Entities/TaskToken.cs
    - server/src/Multica.Core/Entities/DaemonToken.cs
    - server/src/Multica.Core/Entities/PersonalAccessToken.cs
    - server/src/Multica.Infrastructure/Data/MulticaDbContext.cs
    - server/src/Multica.Infrastructure/Data/Configurations/IssueConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/UserConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/WorkspaceConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/MemberConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/TaskTokenConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/DaemonTokenConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/PersonalAccessTokenConfiguration.cs
    - server/src/Multica.Infrastructure/Redis/RedisConnectionProvider.cs
  modified:
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Api/Middleware/DaemonAuthMiddleware.cs
    - .gitignore

key-decisions:
  - "Used static extension method pattern for endpoint registration (MapIssueEndpoints)"
  - "Computed identifier as {workspacePrefix}-{number} matching Go format"
  - "Default metadata to empty object when null for frontend compatibility"
  - "Added all missing entities (User, Workspace, Member, TaskToken, DaemonToken, PAT) for auth context"

patterns-established:
  - "Handler pattern: static class with Map*Endpoints extension method"
  - "DTO pattern: record types with JsonPropertyName attributes for snake_case"
  - "Entity configuration: IEntityTypeConfiguration<T> with explicit column names"

requirements-completed: [ISSUE-01]

# Metrics
duration: 25min
completed: 2026-05-29
---

# Phase 3 Plan 01: Issue CRUD Endpoints Summary

**Issue CRUD API with EF Core entity configuration, matching Go's JSON response format exactly**

## Performance

- **Duration:** 25 min
- **Started:** 2026-05-29T00:48:31Z
- **Completed:** 2026-05-29T01:13:31Z
- **Tasks:** 3
- **Files modified:** 20

## Accomplishments
- Created IssueHandler with full CRUD endpoints (POST, GET, PATCH, DELETE)
- Implemented IssueResponse DTO with snake_case JSON properties matching Go format
- Added Issue entity with EF Core configuration and all required indexes
- Created MulticaDbContext with all entity sets for auth and issue domains
- Registered issue endpoints in Program.cs middleware pipeline

## Task Commits

Each task was committed atomically:

1. **Task 1: Update Issue Entity Configuration** - `a605b39b` (feat)
2. **Task 2: Create Issue Handler with CRUD Endpoints** - `3644d3e2` (feat)
3. **Task 3: Register Issue Endpoints in Program.cs** - `5bddcaed` (feat)

## Files Created/Modified
- `server/src/Multica.Api/Handlers/IssueHandler.cs` - Issue CRUD endpoints with DTOs
- `server/src/Multica.Core/Entities/Issue.cs` - Issue entity matching Go struct
- `server/src/Multica.Core/Entities/User.cs` - User entity for auth context
- `server/src/Multica.Core/Entities/Workspace.cs` - Workspace entity for multi-tenancy
- `server/src/Multica.Core/Entities/Member.cs` - Member entity for workspace membership
- `server/src/Multica.Core/Entities/TaskToken.cs` - Task token entity for auth
- `server/src/Multica.Core/Entities/DaemonToken.cs` - Daemon token entity for auth
- `server/src/Multica.Core/Entities/PersonalAccessToken.cs` - PAT entity for auth
- `server/src/Multica.Infrastructure/Data/MulticaDbContext.cs` - DbContext with all entity sets
- `server/src/Multica.Infrastructure/Data/Configurations/IssueConfiguration.cs` - Issue config with indexes
- `server/src/Multica.Infrastructure/Data/Configurations/UserConfiguration.cs` - User config
- `server/src/Multica.Infrastructure/Data/Configurations/WorkspaceConfiguration.cs` - Workspace config
- `server/src/Multica.Infrastructure/Data/Configurations/MemberConfiguration.cs` - Member config
- `server/src/Multica.Infrastructure/Data/Configurations/TaskTokenConfiguration.cs` - Task token config
- `server/src/Multica.Infrastructure/Data/Configurations/DaemonTokenConfiguration.cs` - Daemon token config
- `server/src/Multica.Infrastructure/Data/Configurations/PersonalAccessTokenConfiguration.cs` - PAT config
- `server/src/Multica.Infrastructure/Redis/RedisConnectionProvider.cs` - Redis connection provider
- `server/src/Multica.Api/Program.cs` - Added MapIssueEndpoints() registration
- `server/src/Multica.Api/Middleware/DaemonAuthMiddleware.cs` - Fixed DateTimeOffset handling
- `.gitignore` - Added exception for C# Data directory

## Decisions Made
- Used static extension method pattern for endpoint registration (MapIssueEndpoints) - consistent with auth endpoints
- Computed identifier as {workspacePrefix}-{number} matching Go format exactly
- Default metadata to empty object when null for frontend compatibility (no nil-guarding needed)
- Added all missing entities (User, Workspace, Member, TaskToken, DaemonToken, PAT) that were supposed to be in Phase 1

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created missing entity files from Phase 1**
- **Found during:** Task 1 (Update Issue Entity Configuration)
- **Issue:** Plan referenced Issue.cs, MulticaDbContext.cs, and other entity files that didn't exist. STATE.md indicated Phase 1 created them, but no commits existed.
- **Fix:** Created all missing entities (Issue, User, Workspace, Member, TaskToken, DaemonToken, PersonalAccessToken) and MulticaDbContext
- **Files modified:** 10 new entity/configuration files created
- **Verification:** Build succeeds with all entities
- **Committed in:** a605b39b (Task 1 commit)

**2. [Rule 3 - Blocking] Fixed .gitignore blocking C# Data directory**
- **Found during:** Task 1 (Update Issue Entity Configuration)
- **Issue:** .gitignore had `data/` rule that blocked tracking `server/src/Multica.Infrastructure/Data/` directory
- **Fix:** Added exception for C# Data directory in .gitignore
- **Files modified:** .gitignore
- **Verification:** Git status shows Data directory as untracked
- **Committed in:** a605b39b (Task 1 commit)

**3. [Rule 1 - Bug] Fixed IssueHandler request DTO parsing**
- **Found during:** Task 2 (Create Issue Handler)
- **Issue:** CreateIssueRequest used string? for AssigneeId, ParentIssueId, ProjectId but code tried to use .HasValue/.Value like nullable Guid
- **Fix:** Changed parsing to use string.IsNullOrEmpty() check instead
- **Files modified:** server/src/Multica.Api/Handlers/IssueHandler.cs
- **Verification:** Build succeeds
- **Committed in:** 3644d3e2 (Task 2 commit)

**4. [Rule 1 - Bug] Fixed DaemonAuthMiddleware DateTimeOffset handling**
- **Found during:** Task 2 (Create Issue Handler)
- **Issue:** DaemonAuthMiddleware tried to use .DateTime on DateTimeOffset? which doesn't exist
- **Fix:** Changed to use ?.DateTime null-conditional operator
- **Files modified:** server/src/Multica.Api/Middleware/DaemonAuthMiddleware.cs
- **Verification:** Build succeeds
- **Committed in:** 3644d3e2 (Task 2 commit)

---

**Total deviations:** 4 auto-fixed (2 blocking, 2 bugs)
**Impact on plan:** All auto-fixes necessary for correctness and build success. Created missing Phase 1 assets as Rule 3 deviation.

## Issues Encountered
- Phase 1 entities and DbContext were missing despite STATE.md indicating completion - created as Rule 3 deviation
- .gitignore blocked C# Data directory - added exception

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Issue CRUD endpoints ready for comment endpoints (Plan 02)
- MulticaDbContext ready for all subsequent plans
- Entity configuration pattern established for future entities

---
*Phase: 03-issues-comments*
*Completed: 2026-05-29*
