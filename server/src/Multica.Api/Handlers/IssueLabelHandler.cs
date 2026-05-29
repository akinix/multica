using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue label management endpoints.
/// Handles attaching/detaching labels to/from issues.
/// </summary>
public static class IssueLabelHandler
{
    /// <summary>
    /// Maps issue label endpoints to the application.
    /// </summary>
    public static void MapIssueLabelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues");

        group.MapPost("/{id}/labels", AddIssueLabel);
        group.MapDelete("/{id}/labels/{labelId}", RemoveIssueLabel);
    }

    /// <summary>
    /// Attaches a label to an issue.
    /// POST /api/issues/{id}/labels
    /// </summary>
    private static async Task<IResult> AddIssueLabel(
        string id,
        [Microsoft.AspNetCore.Mvc.FromBody] AttachLabelRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Validate label_id
        if (string.IsNullOrEmpty(request.LabelId) || !Guid.TryParse(request.LabelId, out var labelId))
        {
            return Results.BadRequest(new { error = "label_id is required and must be a valid UUID" });
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
                .FirstOrDefaultAsync(i => i.WorkspaceId == workspaceId.Value && i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Validate label exists in workspace
        var label = await db.IssueLabels
            .FirstOrDefaultAsync(l => l.Id == labelId && l.WorkspaceId == workspaceId.Value);
        if (label is null)
        {
            return Results.NotFound(new { error = "label not found" });
        }

        // Check if already attached
        var exists = await db.IssueToLabels
            .AnyAsync(il => il.IssueId == issue.Id && il.LabelId == labelId);
        if (exists)
        {
            // Already attached, return current labels
            var existingLabels = await LoadLabelsForIssue(db, issue.Id, workspaceId.Value);
            return Results.Ok(new { labels = existingLabels });
        }

        // Create join record
        var issueToLabel = new IssueToLabel
        {
            IssueId = issue.Id,
            LabelId = labelId
        };
        db.IssueToLabels.Add(issueToLabel);
        await db.SaveChangesAsync();

        // Return updated labels
        var labels = await LoadLabelsForIssue(db, issue.Id, workspaceId.Value);

        logger.LogInformation("Label {LabelId} attached to issue {IssueId}", labelId, issue.Id);

        return Results.Ok(new { labels });
    }

    /// <summary>
    /// Removes a label from an issue.
    /// DELETE /api/issues/{id}/labels/{labelId}
    /// </summary>
    private static async Task<IResult> RemoveIssueLabel(
        string id,
        string labelId,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Validate label_id
        if (!Guid.TryParse(labelId, out var labelUuid))
        {
            return Results.BadRequest(new { error = "invalid label id" });
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
                .FirstOrDefaultAsync(i => i.WorkspaceId == workspaceId.Value && i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Find and remove the join record
        var joinRecord = await db.IssueToLabels
            .FirstOrDefaultAsync(il => il.IssueId == issue.Id && il.LabelId == labelUuid);
        if (joinRecord is null)
        {
            return Results.NoContent();
        }

        db.IssueToLabels.Remove(joinRecord);
        await db.SaveChangesAsync();

        logger.LogInformation("Label {LabelId} removed from issue {IssueId}", labelUuid, issue.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// Bulk loads labels for multiple issues. Returns a dictionary keyed by issue ID string.
    /// </summary>
    public static async Task<Dictionary<string, List<LabelResponse>>> LoadLabelsForIssues(
        MulticaDbContext db,
        List<Guid> issueIds,
        Guid workspaceId)
    {
        var result = new Dictionary<string, List<LabelResponse>>();
        if (issueIds.Count == 0)
        {
            return result;
        }

        var labels = await db.IssueToLabels
            .AsNoTracking()
            .Where(il => issueIds.Contains(il.IssueId))
            .Join(db.IssueLabels,
                il => il.LabelId,
                l => l.Id,
                (il, l) => new { il.IssueId, Label = l })
            .Where(x => x.Label.WorkspaceId == workspaceId)
            .ToListAsync();

        foreach (var item in labels)
        {
            var issueIdStr = item.IssueId.ToString();
            if (!result.ContainsKey(issueIdStr))
            {
                result[issueIdStr] = new List<LabelResponse>();
            }
            result[issueIdStr].Add(new LabelResponse
            {
                Id = item.Label.Id.ToString(),
                WorkspaceId = item.Label.WorkspaceId.ToString(),
                Name = item.Label.Name,
                Color = item.Label.Color,
                CreatedAt = item.Label.CreatedAt.ToString("o"),
                UpdatedAt = item.Label.UpdatedAt.ToString("o")
            });
        }

        return result;
    }

    /// <summary>
    /// Loads labels for a single issue.
    /// </summary>
    public static async Task<List<LabelResponse>> LoadLabelsForIssue(
        MulticaDbContext db,
        Guid issueId,
        Guid workspaceId)
    {
        return await db.IssueToLabels
            .AsNoTracking()
            .Where(il => il.IssueId == issueId)
            .Join(db.IssueLabels,
                il => il.LabelId,
                l => l.Id,
                (il, l) => l)
            .Where(l => l.WorkspaceId == workspaceId)
            .OrderBy(l => l.Name)
            .Select(l => new LabelResponse
            {
                Id = l.Id.ToString(),
                WorkspaceId = l.WorkspaceId.ToString(),
                Name = l.Name,
                Color = l.Color,
                CreatedAt = l.CreatedAt.ToString("o"),
                UpdatedAt = l.UpdatedAt.ToString("o")
            })
            .ToListAsync();
    }

    // DTOs

    public record AttachLabelRequest
    {
        [JsonPropertyName("label_id")]
        public string LabelId { get; init; } = "";
    }
}
