---
phase: 03-issues-comments
plan: 04
subsystem: api
tags: [issues, batch-operations, ef-core, minimal-api]

# Dependency graph
requires:
  - phase: 03-issues-comments
    plan: 01
    provides: [issue-crud, issue-handler, db-context]
provides:
  - Batch update endpoint (POST /api/issues/batch/update)
  - Batch delete endpoint (POST /api/issues/batch/delete)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: [raw-json-field-detection, batch-operations]

key-files:
  created: []
  modified:
    - server/src/Multica.Api/Handlers/IssueHandler.cs

key-decisions:
  - "Used raw JSON parsing for batch update to distinguish 'not present' vs 'explicitly null' fields (matching Go behavior)"
  - "Batch update applies all changes in single SaveChangesAsync call (no per-issue transaction)"
  - "Batch delete silently skips non-existent IDs per D-10"
  - "Max batch size 100 per D-12"

patterns-established:
  - "Batch endpoint pattern: parse JSON body manually, loop through IDs, skip invalid/missing"

requirements-completed: [ISSUE-03]

# Metrics
duration: 15min
completed: 2026-05-29
---

# Phase 3 Plan 04: Batch Issue Operations Summary

**Batch update and delete endpoints for issues, matching Go's behavior exactly**

## Performance

- **Duration:** 15 min
- **Started:** 2026-05-29T01:37:30Z
- **Completed:** 2026-05-29T01:52:30Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Added `POST /api/issues/batch/update` endpoint with raw JSON field detection
- Added `POST /api/issues/batch/delete` endpoint with silent skip of invalid IDs
- Implemented batch size validation (max 100 per D-12)
- Used raw JSON parsing to match Go's behavior for distinguishing "not present" vs "explicitly null" fields
- Batch update applies only explicitly set fields to each issue
- Batch delete silently skips non-existent IDs and returns count

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement Batch Update and Delete Endpoints** - `56a8cb0c` (feat)

## Files Created/Modified

- `server/src/Multica.Api/Handlers/IssueHandler.cs` - Added batch endpoints with DTOs

## Decisions Made

- Used raw JSON parsing (`JsonElement`) for batch update to detect explicitly set fields, matching Go's `json.RawMessage` approach
- Batch update applies all changes in single `SaveChangesAsync` call (no per-issue transaction, matching Go behavior)
- Batch delete silently skips non-existent IDs per D-10
- Max batch size 100 per D-12
- Batch update short-circuits with `{"updated": 0}` when no mutation fields present (matching Go's #1660 fix)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Refactored batch update to use raw JSON parsing**
- **Found during:** Task 1
- **Issue:** Initial implementation used typed DTOs for batch update fields, but this couldn't distinguish between "field not present" and "field explicitly set to null". Go's behavior uses raw JSON detection for nullable fields like assignee_type, assignee_id, start_date, due_date, parent_issue_id, project_id.
- **Fix:** Changed batch update endpoint to accept `JsonElement` body and parse fields manually, matching Go's behavior exactly
- **Files modified:** server/src/Multica.Api/Handlers/IssueHandler.cs
- **Verification:** Build succeeds with 0 errors

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Deviation necessary for API compatibility with Go backend.

## Issues Encountered

- None significant. Build succeeded on first attempt after refactoring.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Batch endpoints ready for frontend integration
- Pattern established for future batch operations

---
*Phase: 03-issues-comments*
*Completed: 2026-05-29*

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.
