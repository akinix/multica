---
phase: 3
plan: 09
subsystem: issues-comments
tags: [subscriptions, task-management, endpoints]
depends_on:
  requires: [03-01]
  provides: [issue-subscriptions, issue-task-stubs]
  affects: [issue-workflow]
tech_stack:
  added: []
  patterns: [handler-pattern, ef-core-configuration]
key_files:
  created:
    - server/src/Multica.Api/Handlers/SubscriberHandler.cs
    - server/src/Multica.Api/Handlers/IssueTaskHandler.cs
    - server/src/Multica.Infrastructure/Data/Configurations/IssueSubscriberConfiguration.cs
  modified:
    - server/src/Multica.Api/Handlers/IssueHandler.cs
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Infrastructure/Data/MulticaDbContext.cs
decisions:
  - "Made IssueToResponse public for cross-handler reuse"
  - "Task endpoints are stubs that update status directly (full TaskService integration in Phase 7)"
metrics:
  duration: ~5 minutes
  completed: "2026-05-29"
  tasks_completed: 2
  tasks_total: 2
---

# Phase 3 Plan 09: Issue Task Management & Subscriptions Summary

Issue subscription management and task lifecycle endpoints with EF Core integration.

## What Was Built

### Issue Subscription Endpoints (SubscriberHandler.cs)

- **GET /api/issues/{id}/subscribers** - Lists all subscribers for an issue
- **POST /api/issues/{id}/subscribe** - Subscribes a user to an issue (defaults to caller)
- **POST /api/issues/{id}/unsubscribe** - Unsubscribes a user from an issue (defaults to caller)

### Issue Task Management Stubs (IssueTaskHandler.cs)

- **POST /api/issues/{id}/claim** - Claims an issue (transitions todo/backlog to in_progress)
- **POST /api/issues/{id}/start** - Starts an issue (transitions to in_progress)
- **POST /api/issues/{id}/complete** - Completes an issue (transitions to done)
- **POST /api/issues/{id}/cancel** - Cancels an issue (transitions to cancelled)

### Infrastructure Changes

- Added `DbSet<IssueSubscriber>` to MulticaDbContext
- Created IssueSubscriberConfiguration for EF Core entity mapping
- Made IssueToResponse public for cross-handler reuse

## Deviations from Plan

None - plan executed exactly as written.

## Key Decisions

1. **Made IssueToResponse public** - Required for IssueTaskHandler to reuse the response building logic from IssueHandler
2. **Task endpoints are stubs** - Full task lifecycle (enqueue, dispatch, heartbeat, complete) will be implemented in Phase 7 with TaskService integration

## Verification

- Build succeeds with 0 errors
- All endpoints registered in Program.cs
- EF Core configuration maps to issue_subscriber table
- Composite primary key (issue_id, user_type, user_id) configured

## Self-Check: PASSED

- SubscriberHandler.cs: FOUND
- IssueTaskHandler.cs: FOUND
- IssueSubscriberConfiguration.cs: FOUND
- Commit 76efe7fa: FOUND
