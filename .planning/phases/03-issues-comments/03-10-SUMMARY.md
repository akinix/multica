---
phase: 3
plan: 10
subsystem: comments
tags: [comments, resolve, timeline, api]
depends_on:
  requires: [03-02, 03-07, 03-08]
  provides: [comment-resolve, comment-timeline]
  affects: [CommentHandler]
tech_stack:
  added: []
  patterns: [actor-resolution, bulk-loading]
key_files:
  created: []
  modified: [server/src/Multica.Api/Handlers/CommentHandler.cs]
decisions:
  - "Use X-Actor-Source header to determine actor type (member vs agent)"
  - "Timeline endpoint returns pure comment list per D-16, no activity log merging"
  - "Re-resolve and re-unresolve are no-ops returning current state"
metrics:
  duration_seconds: 120
  completed_at: "2026-05-29"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 1
---

# Phase 3 Plan 10: Comment Resolve/Unresolve & Timeline Summary

Comment thread resolution and timeline view implemented - resolve/unresolve for root comments with actor tracking, chronological timeline endpoint with reactions and attachments.

## What Was Built

### Comment Resolve/Unresolve
- **POST /api/comments/{commentId}/resolve** - Resolves a root comment thread
  - Only root comments (ParentId == null) can be resolved
  - Sets ResolvedAt, ResolvedByType, ResolvedById
  - Supports both member and agent actors via X-Actor-Source header
  - Re-resolve is a no-op (returns current state)

- **POST /api/comments/{commentId}/unresolve** - Unresolves a root comment thread
  - Clears ResolvedAt, ResolvedByType, ResolvedById
  - Re-unresolve is a no-op (returns current state)

### Comment Timeline
- **GET /api/issues/{issueId}/comments/timeline** - Chronological comment list
  - Returns comments sorted by CreatedAt ASC
  - Includes reactions and attachments for each comment
  - Supports `since` parameter for incremental polling (RFC3339 format)
  - Hard cap at 2000 comments

### Shared Helpers
- **LoadRootCommentForActor** - Validates workspace access, loads comment, checks is root, resolves actor
- **EnrichCommentResponse** - Bulk loads reactions and attachments for a single comment

## Key Implementation Details

### Actor Resolution
The actor type (member vs agent) is determined by the X-Actor-Source header set by auth middleware:
- `task_token` → agent (uses X-Agent-ID)
- Otherwise → member (uses X-User-ID)

### Response Format
All comment responses include:
- reactions: Array of ReactionResponse
- attachments: Array of AttachmentResponse
- resolved_at, resolved_by_type, resolved_by_id (for resolve/unresolve)

## Files Modified

- `server/src/Multica.Api/Handlers/CommentHandler.cs` - Added 3 endpoints, 2 helpers

## Verification

Build succeeds: `dotnet build server/src/Multica.Api --no-restore`

## Self-Check: PASSED
