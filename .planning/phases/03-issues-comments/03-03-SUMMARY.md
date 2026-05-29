---
phase: 03-issues-comments
plan: 03
subsystem: api
tags: [issues, search, filters, grouped-listing, ef-core, minimal-api]

# Dependency graph
requires:
  - phase: 03-issues-comments
    provides: [issue-crud, comment-crud, multica-db-context]
provides:
  - Issue search endpoint with full-text matching and ranking
  - Issue list endpoint with filtering, sorting, and pagination
  - Grouped issue listing by assignee
  - SearchIssueResponse DTO with match metadata and snippets
  - GroupedIssuesResponse DTO
affects: [03-04-batch-operations]

# Tech tracking
tech-stack:
  added: []
  patterns: [dynamic-linq-querying, tiered-ranking, snippet-extraction, grouped-response]

key-files:
  created: []
  modified:
    - server/src/Multica.Api/Handlers/IssueHandler.cs

key-decisions:
  - "Used EF Core LINQ for search queries (per D-01 decision)"
  - "Implemented 9-tier ranking system matching Go's tiered ranking"
  - "Snippet extraction uses ~120 chars centered on match"
  - "Grouped listing uses in-memory grouping after materialization"
  - "Static routes registered before parameterized routes to avoid conflicts"

patterns-established:
  - "Search pattern: LINQ-based full-text search with multi-word AND logic"
  - "Ranking pattern: ComputeRank with tiered priority"
  - "Grouping pattern: Materialize then GroupBy in memory"

requirements-completed: [ISSUE-02, ISSUE-11]

# Metrics
duration: 5min
completed: 2026-05-29
---

# Phase 3 Plan 03: Issue Search, Filters & Grouped Listing Summary

**Issue search with full-text matching, 9-tier ranking, filter support, and grouped listing by assignee**

## Performance

- **Duration:** 5 min
- **Started:** 2026-05-29T01:25:55Z
- **Completed:** 2026-05-29T01:31:11Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- Implemented SearchIssues endpoint (GET /api/issues/search) with full-text search across title, description, and comments
- Added multi-word AND logic for search queries
- Implemented issue number pattern matching (MUL-123 or bare 123)
- Created 9-tier ranking system matching Go's tiered ranking (number > exact title > starts-with > title contains > all words in title > description > all words in description > comment > all words in comment)
- Added snippet extraction (~120 chars centered on match) for display
- Implemented ListIssues endpoint (GET /api/issues) with full filter support
- Supported filters: status, priority, assignee_id, assignee_ids, creator_id, project_id, open_only, scheduled
- Supported sorting by position, title, created_at, start_date, due_date, priority
- Implemented ListGroupedIssues endpoint (GET /api/issues/grouped) with group_by=assignee
- Grouped response uses format "assignee:{type}:{id}" or "assignee:unassigned"
- All responses include total count and match Go's JSON format exactly

## Task Commits

Both tasks were implemented in a single commit due to tight coupling:

1. **Task 1 & 2: SearchIssues, ListIssues, ListGroupedIssues** - `bec9cefb` (feat)

## Files Created/Modified

- `server/src/Multica.Api/Handlers/IssueHandler.cs` - Added search, list, and grouped list endpoints with DTOs

## Decisions Made

- Used EF Core LINQ for search queries instead of raw SQL (per D-01 decision from CONTEXT.md)
- Implemented 9-tier ranking system matching Go's tiered ranking exactly
- Snippet extraction uses ~120 chars centered on match for display
- Grouped listing materializes all matching issues then groups in memory (simpler than SQL window functions)
- Static routes (/api/issues/search, /api/issues/grouped, /api/issues) registered before parameterized routes (/{id}) to avoid routing conflicts

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Initial build failed due to NuGet packages not restored (worktree issue) - resolved with `dotnet restore`
- SearchIssueResponse used `init` properties but tried to assign after construction - fixed by computing snippets before object initializer

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Search and list endpoints ready for batch operations (Plan 04)
- Grouped listing pattern established for future grouping needs
- All endpoints match Go's JSON response format exactly

---

*Phase: 03-issues-comments*
*Completed: 2026-05-29*

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.
