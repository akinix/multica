---
phase: 3
plan: 07
subsystem: issues-comments
tags: [reactions, api, endpoints]
depends_on: [03-01, 03-02]
tech_stack:
  added: []
  patterns: [EF Core configurations, bulk loading pattern]
key_files:
  created:
    - server/src/Multica.Api/Handlers/IssueReactionHandler.cs
    - server/src/Multica.Api/Handlers/ReactionHandler.cs
    - server/src/Multica.Infrastructure/Data/Configurations/IssueReactionConfiguration.cs
    - server/src/Multica.Infrastructure/Data/Configurations/CommentReactionConfiguration.cs
  modified:
    - server/src/Multica.Api/Handlers/CommentHandler.cs
    - server/src/Multica.Api/Handlers/IssueHandler.cs
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Infrastructure/Data/MulticaDbContext.cs
decisions:
  - "Used Go-compatible reaction response format with individual reaction records"
  - "Implemented bulk loading pattern for reactions (same as attachments)"
metrics:
  duration: "5m"
  completed: "2026-05-29"
  tasks_completed: 2
  files_created: 4
  files_modified: 4
---

# Phase 3 Plan 07: Issue & Comment Reactions Summary

Implemented reaction endpoints for issues and comments with Go-compatible response format.

## What Was Built

### Issue Reaction Endpoints
- **POST /api/issues/{id}/reactions** - Add reaction to issue (returns 201)
- **DELETE /api/issues/{id}/reactions** - Remove reaction from issue (returns 204)
- **GET /api/issues/{id}** - Now includes `reactions` array in response

### Comment Reaction Endpoints
- **POST /api/comments/{commentId}/reactions** - Add reaction to comment (returns 201)
- **DELETE /api/comments/{commentId}/reactions** - Remove reaction from comment (returns 204)
- **GET /api/issues/{id}/comments** - Now includes `reactions` array for each comment

### Database Support
- EF configurations for `IssueReaction` and `CommentReaction` entities
- Proper indexes for bulk loading and unique constraints
- Cascade delete from parent entities (issue/comment)

## Technical Details

### Response Format
Both reaction endpoints return individual reaction records matching Go's format:
```json
{
  "id": "uuid",
  "issue_id": "uuid",  // or "comment_id" for comment reactions
  "actor_type": "member",
  "actor_id": "uuid",
  "emoji": "👍",
  "created_at": "2026-05-29T12:00:00Z"
}
```

### Bulk Loading
Reactions are bulk-loaded for list endpoints (comments) using the same pattern as attachments:
- Single entity: `LoadReactionsForIssue()` / `LoadReactionsForComment()`
- Multiple entities: `LoadReactionsForIssues()` / `LoadReactionsForComments()`

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None - all endpoints are fully functional.

## Verification

Build succeeds with `dotnet build server/src/Multica.Api --no-restore`.

## Self-Check: PASSED

All files created and modified as specified in the plan.
