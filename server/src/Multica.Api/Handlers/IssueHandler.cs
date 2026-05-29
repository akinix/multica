using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Api.Middleware;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue API endpoints for CRUD operations.
/// Compatible with Go's issue handler implementation.
/// </summary>
public static class IssueHandler
{
    /// <summary>
    /// Maps issue-related endpoints to the application.
    /// </summary>
    public static void MapIssueEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues");

        group.MapPost("/", CreateIssue);
        group.MapGet("/{id}", GetIssue);
        group.MapPatch("/{id}", UpdateIssue);
        group.MapDelete("/{id}", DeleteIssue);
    }

    /// <summary>
    /// Creates a new issue.
    /// </summary>
    private static async Task<IResult> CreateIssue(
        [FromBody] CreateIssueRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        // Validate title
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "title is required" });
        }

        // Get workspace from context
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Get creator from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var creatorId))
        {
            return Results.Unauthorized();
        }

        // Default status and priority
        var status = string.IsNullOrEmpty(request.Status) ? "todo" : request.Status;
        var priority = string.IsNullOrEmpty(request.Priority) ? "none" : request.Priority;

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        if (workspace is null)
        {
            return Results.BadRequest(new { error = "workspace not found" });
        }

        // Generate issue number (use max + 1)
        var maxNumber = await db.Issues
            .Where(i => i.WorkspaceId == workspaceId.Value)
            .MaxAsync(i => (int?)i.Number) ?? 0;
        var issueNumber = maxNumber + 1;

        // Create issue entity
        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId.Value,
            Title = request.Title,
            Description = request.Description,
            Status = status,
            Priority = priority,
            AssigneeType = request.AssigneeType,
            AssigneeId = !string.IsNullOrEmpty(request.AssigneeId) ? Guid.Parse(request.AssigneeId) : null,
            CreatorType = "member",
            CreatorId = creatorId,
            ParentIssueId = !string.IsNullOrEmpty(request.ParentIssueId) ? Guid.Parse(request.ParentIssueId) : null,
            ProjectId = !string.IsNullOrEmpty(request.ProjectId) ? Guid.Parse(request.ProjectId) : null,
            Position = 0,
            Number = issueNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Parse dates if provided
        if (!string.IsNullOrEmpty(request.StartDate) && DateTimeOffset.TryParse(request.StartDate, out var startDate))
        {
            issue.StartDate = startDate;
        }
        if (!string.IsNullOrEmpty(request.DueDate) && DateTimeOffset.TryParse(request.DueDate, out var dueDate))
        {
            issue.DueDate = dueDate;
        }

        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        // Build response
        var prefix = workspace.IssuePrefix ?? GenerateIssuePrefix(workspace.Name);
        var response = IssueToResponse(issue, prefix);

        logger.LogInformation("Issue created: {IssueId} with identifier {Identifier}", issue.Id, response.Identifier);

        return Results.Created($"/api/issues/{issue.Id}", response);
    }

    /// <summary>
    /// Gets an issue by ID or identifier (e.g., MUL-123).
    /// </summary>
    private static async Task<IResult> GetIssue(
        string id,
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        Issue? issue;

        // Try to parse as UUID first
        if (Guid.TryParse(id, out var uuid))
        {
            issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == uuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            // Try to parse as identifier (PREFIX-NUMBER)
            var parts = id.Split('-', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            {
                return Results.BadRequest(new { error = "invalid issue id or identifier" });
            }

            var prefix = parts[0];
            issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var issuePrefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");

        var response = IssueToResponse(issue, issuePrefix);
        return Results.Ok(response);
    }

    /// <summary>
    /// Updates an issue (partial update).
    /// </summary>
    private static async Task<IResult> UpdateIssue(
        string id,
        [FromBody] JsonElement body,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Find issue
        Issue? issue;
        if (Guid.TryParse(id, out var uuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == uuid && i.WorkspaceId == workspaceId.Value);
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

        // Apply partial updates
        if (body.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
        {
            issue.Title = titleProp.GetString()!;
        }
        if (body.TryGetProperty("description", out var descProp))
        {
            issue.Description = descProp.ValueKind == JsonValueKind.Null ? null : descProp.GetString();
        }
        if (body.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
        {
            issue.Status = statusProp.GetString()!;
        }
        if (body.TryGetProperty("priority", out var priorityProp) && priorityProp.ValueKind == JsonValueKind.String)
        {
            issue.Priority = priorityProp.GetString()!;
        }
        if (body.TryGetProperty("assignee_type", out var assigneeTypeProp))
        {
            issue.AssigneeType = assigneeTypeProp.ValueKind == JsonValueKind.Null ? null : assigneeTypeProp.GetString();
        }
        if (body.TryGetProperty("assignee_id", out var assigneeIdProp))
        {
            issue.AssigneeId = assigneeIdProp.ValueKind == JsonValueKind.Null ? null : Guid.Parse(assigneeIdProp.GetString()!);
        }
        if (body.TryGetProperty("position", out var positionProp) && positionProp.ValueKind == JsonValueKind.Number)
        {
            issue.Position = positionProp.GetDouble();
        }
        if (body.TryGetProperty("start_date", out var startDateProp))
        {
            if (startDateProp.ValueKind == JsonValueKind.Null)
            {
                issue.StartDate = null;
            }
            else if (DateTimeOffset.TryParse(startDateProp.GetString(), out var startDate))
            {
                issue.StartDate = startDate;
            }
        }
        if (body.TryGetProperty("due_date", out var dueDateProp))
        {
            if (dueDateProp.ValueKind == JsonValueKind.Null)
            {
                issue.DueDate = null;
            }
            else if (DateTimeOffset.TryParse(dueDateProp.GetString(), out var dueDate))
            {
                issue.DueDate = dueDate;
            }
        }
        if (body.TryGetProperty("parent_issue_id", out var parentIdProp))
        {
            issue.ParentIssueId = parentIdProp.ValueKind == JsonValueKind.Null ? null : Guid.Parse(parentIdProp.GetString()!);
        }
        if (body.TryGetProperty("project_id", out var projectIdProp))
        {
            issue.ProjectId = projectIdProp.ValueKind == JsonValueKind.Null ? null : Guid.Parse(projectIdProp.GetString()!);
        }

        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");

        var response = IssueToResponse(issue, prefix);

        logger.LogInformation("Issue updated: {IssueId}", issue.Id);

        return Results.Ok(response);
    }

    /// <summary>
    /// Deletes an issue.
    /// </summary>
    private static async Task<IResult> DeleteIssue(
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

        // Find issue
        Issue? issue;
        if (Guid.TryParse(id, out var uuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == uuid && i.WorkspaceId == workspaceId.Value);
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

        db.Issues.Remove(issue);
        await db.SaveChangesAsync();

        logger.LogInformation("Issue deleted: {IssueId}", issue.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// Converts an Issue entity to an IssueResponse DTO.
    /// </summary>
    private static IssueResponse IssueToResponse(Issue issue, string prefix)
    {
        var identifier = $"{prefix}-{issue.Number}";
        return new IssueResponse
        {
            Id = issue.Id.ToString(),
            WorkspaceId = issue.WorkspaceId.ToString(),
            Number = issue.Number,
            Identifier = identifier,
            Title = issue.Title,
            Description = issue.Description,
            Status = issue.Status,
            Priority = issue.Priority,
            AssigneeType = issue.AssigneeType,
            AssigneeId = issue.AssigneeId?.ToString(),
            CreatorType = issue.CreatorType,
            CreatorId = issue.CreatorId.ToString(),
            ParentIssueId = issue.ParentIssueId?.ToString(),
            ProjectId = issue.ProjectId?.ToString(),
            Position = issue.Position,
            StartDate = issue.StartDate?.ToString("o"),
            DueDate = issue.DueDate?.ToString("o"),
            CreatedAt = issue.CreatedAt.ToString("o"),
            UpdatedAt = issue.UpdatedAt.ToString("o"),
            Metadata = issue.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(issue.Metadata.RootElement.GetRawText())
                : new Dictionary<string, object>()
        };
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

    // DTOs

    public record CreateIssueRequest
    {
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public string? Status { get; init; }
        public string? Priority { get; init; }
        public string? AssigneeType { get; init; }
        public string? AssigneeId { get; init; }
        public string? ParentIssueId { get; init; }
        public string? ProjectId { get; init; }
        public string? StartDate { get; init; }
        public string? DueDate { get; init; }
    }

    public record IssueResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("number")]
        public int Number { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("identifier")]
        public string Identifier { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string? Description { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("priority")]
        public string Priority { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("assignee_type")]
        public string? AssigneeType { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("assignee_id")]
        public string? AssigneeId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("creator_type")]
        public string CreatorType { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("creator_id")]
        public string CreatorId { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("parent_issue_id")]
        public string? ParentIssueId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("project_id")]
        public string? ProjectId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("position")]
        public double Position { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("start_date")]
        public string? StartDate { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("due_date")]
        public string? DueDate { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        public string UpdatedAt { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; init; } = new();
    }
}
