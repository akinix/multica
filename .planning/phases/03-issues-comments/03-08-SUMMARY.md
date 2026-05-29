---
phase: 03-issues-comments
plan: 08
subsystem: api
tags: [attachments, pull-requests, github, ef-core, postgresql]

# Dependency graph
requires:
  - phase: 03-01
    provides: Issue and Comment CRUD endpoints
provides:
  - Attachment listing endpoints for issues and comments
  - Issue-PR linking/unlinking endpoints
  - PR listing per issue
  - Attachments included in GetIssue and ListComments responses
affects: [03-issues-comments, github-integration]

# Tech tracking
tech-stack:
  added: [EF Core configurations for Attachment, IssuePullRequest, GithubPullRequest]
  patterns: [Bulk-loading attachments for comments, Join query for PR listing]

key-files:
  created:
    - server/src/Multica.Api/Handlers/AttachmentHandler.cs
    - server/src/Multica.Api/Handlers/IssuePullRequestHandler.cs
    - server/src/Multica.Infrastructure/Data/Configurations/AttachmentConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/IssuePullRequestConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/GithubPullRequestConfiguration.cs
  modified:
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Api/Handlers/IssueHandler.cs
    - server/src/Multica.Api/Handlers/CommentHandler.cs
    - server/src/Multica.Infrastructure/Data/MulticaDbContext.cs

key-decisions:
  - "Used Join query for PR listing to return full PR details alongside link metadata"
  - "Changed CommentResponse.Attachments from init to set to allow bulk-loading after Select projection"
  - "Added using static import for AttachmentHandler.AttachmentResponse to avoid type conflict with removed CommentHandler.AttachmentResponse"

patterns-established:
  - "Bulk-load pattern: LoadAttachmentsForComments loads all attachments for multiple comment IDs at once, avoiding N+1 queries"
  - "Join pattern: IssuePullRequest listing joins with GithubPullRequest to return full PR details"

requirements-completed: [ISSUE-08, ISSUE-09]

# Metrics
duration: 15min
completed: 2026-05-29
---

# Phase 3 Plan 08: Issue Attachments & PR Tracking Summary

**Attachment listing for issues/comments and pull request linking/unlinking with EF Core configurations**

## Performance

- **Duration:** 15 min
- **Started:** 2026-05-29T10:00:00Z
- **Completed:** 2026-05-29T10:15:00Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- Implemented attachment listing endpoints for issues and comments with Go-compatible response format
- Implemented PR linking/unlinking/listing endpoints with full PR details from github_pull_requests table
- Added EF Core configurations for Attachment, IssuePullRequest, and GithubPullRequest entities
- Updated GetIssue and ListComments to include attachments in responses

## Task Commits

Each task was committed atomically:

1. **Task 1: Attachment Listing Endpoints** - `64d0bc1b` (feat)
2. **Task 2: Issue PR Tracking Endpoints** - `64d0bc1b` (feat, combined with Task 1)

**Plan metadata:** [pending] (docs: complete plan)

## Files Created/Modified
- `server/src/Multica.Api/Handlers/AttachmentHandler.cs` - Attachment listing endpoints with AttachmentResponse DTO matching Go format
- `server/src/Multica.Api/Handlers/IssuePullRequestHandler.cs` - PR link/unlink/list endpoints with IssuePullRequestResponse DTO
- `server/src/Multica.Infrastructure/Data/Configurations/AttachmentConfiguration.cs` - EF Core config for attachments table
- `server/src/Multica.Infrastructure/Data/Configurations/IssuePullRequestConfiguration.cs` - EF Core config for issue_pull_requests join table
- `server/src/Multica.Infrastructure/Data/Configurations/GithubPullRequestConfiguration.cs` - EF Core config for github_pull_requests table
- `server/src/Multica.Api/Program.cs` - Registered MapAttachmentEndpoints and MapIssuePullRequestEndpoints
- `server/src/Multica.Api/Handlers/IssueHandler.cs` - Added Attachments field to IssueResponse, updated GetIssue to load attachments
- `server/src/Multica.Api/Handlers/CommentHandler.cs` - Updated ListComments to bulk-load attachments, removed duplicate AttachmentResponse
- `server/src/Multica.Infrastructure/Data/MulticaDbContext.cs` - Added DbSet for Attachment, IssuePullRequest, GithubPullRequest

## Decisions Made
- Used Join query for PR listing to return full PR details alongside link metadata (matches Go's ListPullRequestsByIssue pattern)
- Changed CommentResponse.Attachments from init to set to allow bulk-loading after Select projection
- Added using static import for AttachmentHandler.AttachmentResponse to avoid type conflict with removed CommentHandler.AttachmentResponse

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added missing EF Core configurations and DbSets**
- **Found during:** Task 1 (Attachment Listing Endpoints)
- **Issue:** Attachment, IssuePullRequest, and GithubPullRequest entities had no EF Core configurations or DbSet entries in MulticaDbContext
- **Fix:** Created AttachmentConfiguration, IssuePullRequestConfiguration, GithubPullRequestConfiguration, and added DbSet entries
- **Files modified:** server/src/Multica.Infrastructure/Data/MulticaDbContext.cs, server/src/Multica.Infrastructure/Data/Configurations/
- **Verification:** Build succeeded with 0 errors
- **Committed in:** 64d0bc1b (Task 1 commit)

**2. [Rule 2 - Missing Critical] Removed duplicate AttachmentResponse from CommentHandler**
- **Found during:** Task 1 (Attachment Listing Endpoints)
- **Issue:** CommentHandler had its own simplified AttachmentResponse (Name, Size) that didn't match Go's full format (Filename, SizeBytes, WorkspaceId, etc.)
- **Fix:** Removed CommentHandler.AttachmentResponse, added using static import for AttachmentHandler.AttachmentResponse
- **Files modified:** server/src/Multica.Api/Handlers/CommentHandler.cs
- **Verification:** Build succeeded, CommentResponse now uses full AttachmentResponse format
- **Committed in:** 64d0bc1b (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing critical)
**Impact on plan:** Both auto-fixes necessary for correctness. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Attachment and PR endpoints ready for use
- GitHub webhook integration can now link PRs to issues automatically
- Frontend can display attachment lists and PR links

## Self-Check: PASSED

All created files verified present:

- server/src/Multica.Api/Handlers/AttachmentHandler.cs
- server/src/Multica.Api/Handlers/IssuePullRequestHandler.cs
- server/src/Multica.Infrastructure/Data/Configurations/AttachmentConfiguration.cs
- server/src/Multica.Infrastructure/Data/Configurations/IssuePullRequestConfiguration.cs
- server/src/Multica.Infrastructure/Data/Configurations/GithubPullRequestConfiguration.cs

All modified files verified present:

- server/src/Multica.Api/Program.cs
- server/src/Multica.Api/Handlers/IssueHandler.cs
- server/src/Multica.Api/Handlers/CommentHandler.cs
- server/src/Multica.Infrastructure/Data/MulticaDbContext.cs

Commit verified: 64d0bc1b

---
*Phase: 03-issues-comments*
*Completed: 2026-05-29*
