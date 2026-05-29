---
phase: 03-issues-comments
plan: 06
subsystem: api
tags: [labels, metadata, jsonb, ef-core, minimal-api]

# Dependency graph
requires:
  - phase: 03-issues-comments
    provides: Issue CRUD endpoints, Issue entity with Metadata JsonDocument column
provides:
  - Issue label attach/detach endpoints
  - Issue metadata KV store endpoints
  - Bulk label loading for issue lists
  - LabelResponse DTO shared across handlers
affects: [03-issues-comments, issue-ui, agent-pipeline]

# Tech tracking
tech-stack:
  added: []
  patterns: [bulk-loading-avoid-n-plus-1, jsonb-kv-store, join-table-configuration]

key-files:
  created:
    - server/src/Multica.Api/Handlers/IssueLabelHandler.cs
    - server/src/Multica.Api/Handlers/IssueMetadataHandler.cs
    - server/src/Multica.Api/Handlers/IssueDtos.cs
    - server/src/Multica.Infrastructure/Data/Configurations/IssueToLabelConfiguration.cs
  modified:
    - server/src/Multica.Api/Handlers/IssueHandler.cs
    - server/src/Multica.Infrastructure/Data/MulticaDbContext.cs
    - server/src/Multica.Api/Program.cs

key-decisions:
  - "Moved LabelResponse to shared IssueDtos.cs to avoid cross-handler type dependency"
  - "Used EF Core LINQ join for bulk label loading instead of raw SQL"

patterns-established:
  - "Bulk loading pattern: LoadLabelsForIssues returns Dictionary<string, List<LabelResponse>> keyed by issue ID"
  - "Metadata validation: regex key pattern, primitive value check, 50-key limit, 8KB size limit"

requirements-completed: [ISSUE-05, ISSUE-06]

# Metrics
duration: 7min
completed: 2026-05-29
---

# Phase 3 Plan 06: Issue Labels & Metadata Summary

**Issue label attach/detach with bulk loading and JSONB metadata KV store with validation**

## Performance

- **Duration:** 7 min
- **Started:** 2026-05-29T01:59:51Z
- **Completed:** 2026-05-29T02:06:25Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Implemented issue label endpoints (POST attach, DELETE detach) with workspace-scoped validation
- Implemented issue metadata KV endpoints (GET list, PUT set key, DELETE remove key) with full validation
- Added bulk label loading to avoid N+1 queries in issue list/detail endpoints
- Added Labels field to IssueResponse DTO with optional inclusion pattern

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement Issue Label Endpoints** - `594ba346` (feat)
2. **Task 2: Implement Issue Metadata Endpoints** - `17a2bc56` (feat)

**Plan metadata:** [pending] (docs: complete plan)

## Files Created/Modified
- `server/src/Multica.Api/Handlers/IssueLabelHandler.cs` - Label attach/detach endpoints with bulk loading helper
- `server/src/Multica.Api/Handlers/IssueMetadataHandler.cs` - Metadata KV endpoints with validation
- `server/src/Multica.Api/Handlers/IssueDtos.cs` - Shared LabelResponse DTO
- `server/src/Multica.Infrastructure/Data/Configurations/IssueToLabelConfiguration.cs` - Join table EF Core config
- `server/src/Multica.Infrastructure/Data/MulticaDbContext.cs` - Added IssueLabel and IssueToLabel DbSets
- `server/src/Multica.Api/Handlers/IssueHandler.cs` - Added Labels field to IssueResponse, bulk label loading in ListIssues
- `server/src/Multica.Api/Program.cs` - Registered label and metadata endpoint groups

## Decisions Made
- Moved LabelResponse to shared IssueDtos.cs file to avoid circular dependency between IssueHandler and IssueLabelHandler
- Used EF Core LINQ join for bulk label loading instead of raw SQL query (consistent with existing patterns)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] LabelResponse type not accessible across handlers**
- **Found during:** Task 1 (Issue Label Endpoints)
- **Issue:** LabelResponse was defined as nested type inside IssueHandler class, not accessible from IssueLabelHandler
- **Fix:** Moved LabelResponse to shared IssueDtos.cs file at namespace level
- **Files modified:** server/src/Multica.Api/Handlers/IssueDtos.cs (created), server/src/Multica.Api/Handlers/IssueHandler.cs (modified)
- **Verification:** Build succeeds with 0 errors
- **Committed in:** 594ba346 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Fix necessary for cross-handler type access. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Label and metadata endpoints ready for use by agent pipeline
- Issue responses now include labels array for UI rendering
- Metadata KV store ready for agent state tracking

---
*Phase: 03-issues-comments*
*Completed: 2026-05-29*
