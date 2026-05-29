using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue task management endpoints for status transitions.
/// These are stubs that will be fully wired to TaskService in Phase 7.
/// Compatible with Go's issue task lifecycle.
/// </summary>
public static class IssueTaskHandler
{
    /// <summary>
    /// Maps issue task management endpoints to the application.
    /// </summary>
    public static void MapIssueTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues/{id}");

        group.MapPost("/claim", ClaimIssue);
        group.MapPost("/start", StartIssue);
        group.MapPost("/complete", CompleteIssue);
        group.MapPost("/cancel", CancelIssue);
    }

    /// <summary>
    /// Claims an issue (transitions to in_progress).
    /// Validates issue is in "todo" or "backlog" status.
    /// POST /api/issues/{id}/claim
    /// </summary>
    private static async Task<IResult> ClaimIssue(
        string id,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue
        Issue? issue;
        if (Guid.TryParse(id, out var issueUuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = id.Split('-', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            {
                return Results.BadRequest(new { error = "invalid issue id or identifier" });
            }
            issue = await db.Issues
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Validate issue is in a claimable status
        if (issue.Status != "todo" && issue.Status != "backlog")
        {
            return Results.BadRequest(new { error = $"cannot claim issue in '{issue.Status}' status" });
        }

        // Update status to in_progress
        issue.Status = "in_progress";
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Issue claimed: {IssueId}", issue.Id);

        // Build response
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");
        var response = IssueHandler.IssueToResponse(issue, prefix);

        return Results.Ok(response);
    }

    /// <summary>
    /// Starts an issue (transitions to in_progress).
    /// POST /api/issues/{id}/start
    /// </summary>
    private static async Task<IResult> StartIssue(
        string id,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue
        Issue? issue;
        if (Guid.TryParse(id, out var issueUuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = id.Split('-', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            {
                return Results.BadRequest(new { error = "invalid issue id or identifier" });
            }
            issue = await db.Issues
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Update status to in_progress
        issue.Status = "in_progress";
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Issue started: {IssueId}", issue.Id);

        // Build response
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");
        var response = IssueHandler.IssueToResponse(issue, prefix);

        return Results.Ok(response);
    }

    /// <summary>
    /// Completes an issue (transitions to done).
    /// POST /api/issues/{id}/complete
    /// </summary>
    private static async Task<IResult> CompleteIssue(
        string id,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue
        Issue? issue;
        if (Guid.TryParse(id, out var issueUuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = id.Split('-', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            {
                return Results.BadRequest(new { error = "invalid issue id or identifier" });
            }
            issue = await db.Issues
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Update status to done
        issue.Status = "done";
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Issue completed: {IssueId}", issue.Id);

        // Build response
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");
        var response = IssueHandler.IssueToResponse(issue, prefix);

        return Results.Ok(response);
    }

    /// <summary>
    /// Cancels an issue (transitions to cancelled).
    /// POST /api/issues/{id}/cancel
    /// </summary>
    private static async Task<IResult> CancelIssue(
        string id,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue
        Issue? issue;
        if (Guid.TryParse(id, out var issueUuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = id.Split('-', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            {
                return Results.BadRequest(new { error = "invalid issue id or identifier" });
            }
            issue = await db.Issues
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Update status to cancelled
        issue.Status = "cancelled";
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Issue cancelled: {IssueId}", issue.Id);

        // Build response
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");
        var response = IssueHandler.IssueToResponse(issue, prefix);

        return Results.Ok(response);
    }

    /// <summary>
    /// Generates an issue prefix from workspace name.
    /// </summary>
    private static string GenerateIssuePrefix(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "ISS";

        // Take first 3 uppercase letters
        var prefix = new string(name
            .Where(char.IsLetter)
            .Take(3)
            .Select(char.ToUpper)
            .ToArray());

        return string.IsNullOrEmpty(prefix) ? "ISS" : prefix;
    }
}
