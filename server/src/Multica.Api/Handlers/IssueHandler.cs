using System.Text.Json;
using System.Text.RegularExpressions;
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
        // Static routes must be registered before parameterized routes to avoid conflicts
        app.MapGet("/api/issues/search", SearchIssues);
        app.MapGet("/api/issues/grouped", ListGroupedIssues);
        app.MapGet("/api/issues", ListIssues);

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

    // --- Search & List Endpoints ---

    private static readonly Regex IdentifierNumberRe = new(@"(?i)^[a-z]+-(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Searches issues by query string across title, description, and comments.
    /// Returns ranked results with match metadata and snippets.
    /// </summary>
    private static async Task<IResult> SearchIssues(
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        var q = httpContext.Request.Query["q"].ToString();
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { error = "q parameter is required" });
        }

        // Parse limit and offset
        var limit = 20;
        if (int.TryParse(httpContext.Request.Query["limit"].ToString(), out var l) && l > 0)
            limit = Math.Min(l, 50);

        var offset = 0;
        if (int.TryParse(httpContext.Request.Query["offset"].ToString(), out var o) && o >= 0)
            offset = o;

        var includeClosed = httpContext.Request.Query["include_closed"].ToString() == "true";

        // Parse search terms
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var queryNum = ParseQueryNumber(q, out var hasNum);
        var lowerQ = q.ToLower();
        var lowerTerms = terms.Select(t => t.ToLower()).ToArray();

        // Build base query
        var issuesQuery = db.Issues
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId.Value);

        // Apply search filter
        issuesQuery = issuesQuery.Where(i =>
            hasNum && i.Number == queryNum
            || i.Title.ToLower().Contains(lowerQ)
            || (i.Description != null && i.Description.ToLower().Contains(lowerQ))
            || db.Comments.Any(c => c.IssueId == i.Id && c.Content.ToLower().Contains(lowerQ))
            || (lowerTerms.Length > 1 && lowerTerms.All(t =>
                i.Title.ToLower().Contains(t)
                || (i.Description != null && i.Description.ToLower().Contains(t))
                || db.Comments.Any(c => c.IssueId == i.Id && c.Content.ToLower().Contains(t)))));

        // Exclude closed unless requested
        if (!includeClosed)
        {
            issuesQuery = issuesQuery.Where(i => i.Status != "done" && i.Status != "cancelled");
        }

        // Materialize for in-memory ranking
        var allMatching = await issuesQuery.ToListAsync();

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");

        // Rank results
        var ranked = allMatching.Select(issue =>
        {
            var rank = ComputeRank(issue, lowerQ, lowerTerms, hasNum, queryNum, db);
            var matchSource = DetermineMatchSource(issue, lowerQ, lowerTerms, db);
            return new { Issue = issue, Rank = rank, MatchSource = matchSource };
        })
        .OrderBy(x => x.Rank)
        .ThenBy(x => StatusPriority(x.Issue.Status))
        .ThenByDescending(x => x.Issue.UpdatedAt)
        .ToList();

        var total = ranked.Count;
        var paged = ranked.Skip(offset).Take(limit).ToList();

        // Build response with snippets
        var searchResults = new List<SearchIssueResponse>();
        foreach (var item in paged)
        {
            var response = IssueToResponse(item.Issue, prefix);

            // Compute snippets before constructing the record
            string? matchedSnippet = null;
            string? matchedDescSnippet = null;
            string? matchedCommentSnippet = null;

            if (item.MatchSource == "title" && item.Issue.Title != null)
            {
                matchedSnippet = ExtractSnippet(item.Issue.Title, q);
            }
            else if (item.MatchSource == "description" && item.Issue.Description != null)
            {
                matchedDescSnippet = ExtractSnippet(item.Issue.Description, q);
                matchedSnippet = matchedDescSnippet;
            }
            else if (item.MatchSource == "comment")
            {
                var commentContent = await db.Comments
                    .Where(c => c.IssueId == item.Issue.Id && c.Content.ToLower().Contains(lowerQ))
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.Content)
                    .FirstOrDefaultAsync();
                if (commentContent != null)
                {
                    matchedCommentSnippet = ExtractSnippet(commentContent, q);
                    matchedSnippet = matchedCommentSnippet;
                }
            }

            searchResults.Add(new SearchIssueResponse
            {
                Issue = response,
                MatchSource = item.MatchSource,
                MatchedSnippet = matchedSnippet,
                MatchedDescriptionSnippet = matchedDescSnippet,
                MatchedCommentSnippet = matchedCommentSnippet
            });
        }

        httpContext.Response.Headers["X-Total-Count"] = total.ToString();
        return Results.Ok(new { issues = searchResults, total });
    }

    /// <summary>
    /// Computes search rank for an issue (lower is better).
    /// Tier 0: Number exact match
    /// Tier 1: Exact title match
    /// Tier 2: Title starts with query
    /// Tier 3: Title contains query
    /// Tier 4: Title matches all words (multi-word)
    /// Tier 5: Description contains query
    /// Tier 6: Description matches all words
    /// Tier 7: Comment contains query
    /// Tier 8: Comment matches all words
    /// Tier 9: No match (fallback)
    /// </summary>
    private static int ComputeRank(Issue issue, string lowerQ, string[] lowerTerms, bool hasNum, int queryNum, MulticaDbContext db)
    {
        // Tier 0: Number exact match
        if (hasNum && issue.Number == queryNum)
            return 0;

        var lowerTitle = issue.Title.ToLower();
        var lowerDesc = issue.Description?.ToLower() ?? "";

        // Tier 1: Exact title match
        if (lowerTitle == lowerQ)
            return 1;

        // Tier 2: Title starts with query
        if (lowerTitle.StartsWith(lowerQ))
            return 2;

        // Tier 3: Title contains query
        if (lowerTitle.Contains(lowerQ))
            return 3;

        // Tier 4: Title matches all words (multi-word)
        if (lowerTerms.Length > 1 && lowerTerms.All(t => lowerTitle.Contains(t)))
            return 4;

        // Tier 5: Description contains query
        if (!string.IsNullOrEmpty(lowerDesc) && lowerDesc.Contains(lowerQ))
            return 5;

        // Tier 6: Description matches all words
        if (lowerTerms.Length > 1 && !string.IsNullOrEmpty(lowerDesc) && lowerTerms.All(t => lowerDesc.Contains(t)))
            return 6;

        // Tier 7: Comment contains query
        if (db.Comments.Any(c => c.IssueId == issue.Id && c.Content.ToLower().Contains(lowerQ)))
            return 7;

        // Tier 8: Comment matches all words
        if (lowerTerms.Length > 1 && lowerTerms.All(t => db.Comments.Any(c => c.IssueId == issue.Id && c.Content.ToLower().Contains(t))))
            return 8;

        return 9;
    }

    /// <summary>
    /// Determines the match source for display.
    /// </summary>
    private static string DetermineMatchSource(Issue issue, string lowerQ, string[] lowerTerms, MulticaDbContext db)
    {
        var lowerTitle = issue.Title.ToLower();
        var lowerDesc = issue.Description?.ToLower() ?? "";

        if (lowerTitle.Contains(lowerQ) || (lowerTerms.Length > 1 && lowerTerms.All(t => lowerTitle.Contains(t))))
            return "title";

        if (!string.IsNullOrEmpty(lowerDesc) && (lowerDesc.Contains(lowerQ) || (lowerTerms.Length > 1 && lowerTerms.All(t => lowerDesc.Contains(t)))))
            return "description";

        return "comment";
    }

    /// <summary>
    /// Maps status to priority for sorting (lower = higher priority).
    /// </summary>
    private static int StatusPriority(string status) => status switch
    {
        "in_progress" => 0,
        "in_review" => 1,
        "todo" => 2,
        "blocked" => 3,
        "backlog" => 4,
        "done" => 5,
        "cancelled" => 6,
        _ => 7
    };

    /// <summary>
    /// Extracts a snippet of ~120 chars centered on the first match of query.
    /// </summary>
    private static string ExtractSnippet(string content, string query)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0 && query.Contains(' '))
        {
            // Try individual terms for multi-word queries
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var earliest = -1;
            foreach (var term in terms)
            {
                var pos = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0 && (earliest < 0 || pos < earliest))
                    earliest = pos;
            }
            idx = earliest;
        }

        if (idx < 0)
        {
            return content.Length > 120 ? content[..120] + "..." : content;
        }

        var start = Math.Max(0, idx - 40);
        var end = Math.Min(content.Length, idx + query.Length + 80);
        var snippet = content[start..end];

        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }

    /// <summary>
    /// Parses query for issue number patterns (MUL-123 or bare 123).
    /// </summary>
    private static int ParseQueryNumber(string q, out bool hasNum)
    {
        q = q.Trim();
        var match = IdentifierNumberRe.Match(q);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > 0)
        {
            hasNum = true;
            return n;
        }
        if (int.TryParse(q, out var bare) && bare > 0)
        {
            hasNum = true;
            return bare;
        }
        hasNum = false;
        return 0;
    }

    /// <summary>
    /// Lists issues with filtering, sorting, and pagination.
    /// </summary>
    private static async Task<IResult> ListIssues(
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Parse filter parameters
        var statusFilter = httpContext.Request.Query["status"].ToString();
        var priorityFilter = httpContext.Request.Query["priority"].ToString();
        var assigneeIdStr = httpContext.Request.Query["assignee_id"].ToString();
        var assigneeIdsStr = httpContext.Request.Query["assignee_ids"].ToString();
        var creatorIdStr = httpContext.Request.Query["creator_id"].ToString();
        var projectIdStr = httpContext.Request.Query["project_id"].ToString();
        var openOnly = httpContext.Request.Query["open_only"].ToString() == "true";
        var scheduled = httpContext.Request.Query["scheduled"].ToString() == "true";

        // Parse pagination
        var limit = 100;
        if (int.TryParse(httpContext.Request.Query["limit"].ToString(), out var l) && l > 0)
            limit = l;

        var offset = 0;
        if (int.TryParse(httpContext.Request.Query["offset"].ToString(), out var o) && o >= 0)
            offset = o;

        // Parse sort
        var sortCol = httpContext.Request.Query["sort"].ToString();
        if (string.IsNullOrEmpty(sortCol)) sortCol = "position";

        var sortDir = httpContext.Request.Query["direction"].ToString().ToUpper();
        if (string.IsNullOrEmpty(sortDir)) sortDir = sortCol == "position" ? "ASC" : "ASC";

        // Build query
        var query = db.Issues
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId.Value);

        // Apply filters
        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(i => i.Status == statusFilter);

        if (!string.IsNullOrEmpty(priorityFilter))
            query = query.Where(i => i.Priority == priorityFilter);

        if (Guid.TryParse(assigneeIdStr, out var assigneeId))
            query = query.Where(i => i.AssigneeId == assigneeId);

        if (!string.IsNullOrEmpty(assigneeIdsStr))
        {
            var ids = assigneeIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            if (ids.Count > 0)
                query = query.Where(i => i.AssigneeId.HasValue && ids.Contains(i.AssigneeId.Value));
        }

        if (Guid.TryParse(creatorIdStr, out var creatorId))
            query = query.Where(i => i.CreatorId == creatorId);

        if (Guid.TryParse(projectIdStr, out var projectId))
            query = query.Where(i => i.ProjectId == projectId);

        if (openOnly)
            query = query.Where(i => i.Status != "done" && i.Status != "cancelled");

        if (scheduled)
            query = query.Where(i => i.StartDate != null || i.DueDate != null);

        // Get total count before pagination
        var total = await query.CountAsync();

        // Apply sorting
        query = sortCol switch
        {
            "title" => sortDir == "DESC" ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            "created_at" => sortDir == "DESC" ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            "start_date" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.StartDate == null).ThenByDescending(i => i.StartDate)
                : query.OrderBy(i => i.StartDate == null).ThenBy(i => i.StartDate),
            "due_date" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.DueDate == null).ThenByDescending(i => i.DueDate)
                : query.OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate),
            "priority" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.Priority == "urgent" ? 0 : i.Priority == "high" ? 1 : i.Priority == "medium" ? 2 : i.Priority == "low" ? 3 : 4)
                : query.OrderBy(i => i.Priority == "urgent" ? 0 : i.Priority == "high" ? 1 : i.Priority == "medium" ? 2 : i.Priority == "low" ? 3 : 4),
            _ => sortDir == "DESC" ? query.OrderByDescending(i => i.Position) : query.OrderBy(i => i.Position),
        };

        // Apply pagination
        var issues = await query
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");

        var response = issues.Select(i => IssueToResponse(i, prefix)).ToList();

        return Results.Ok(new { issues = response, total });
    }

    /// <summary>
    /// Lists issues grouped by assignee.
    /// </summary>
    private static async Task<IResult> ListGroupedIssues(
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        var groupBy = httpContext.Request.Query["group_by"].ToString();
        if (string.IsNullOrEmpty(groupBy)) groupBy = "assignee";
        if (groupBy != "assignee")
        {
            return Results.BadRequest(new { error = "unsupported group_by" });
        }

        // Parse pagination
        var limit = 50;
        if (int.TryParse(httpContext.Request.Query["limit"].ToString(), out var l) && l > 0)
            limit = Math.Min(l, 100);

        var offset = 0;
        if (int.TryParse(httpContext.Request.Query["offset"].ToString(), out var o) && o > 0)
            offset = o;

        // Parse filters
        var statuses = ParseCommaParam(httpContext.Request.Query["statuses"].ToString());
        if (statuses.Count == 0)
            statuses = ParseCommaParam(httpContext.Request.Query["status"].ToString());

        var priorities = ParseCommaParam(httpContext.Request.Query["priorities"].ToString());
        if (priorities.Count == 0)
            priorities = ParseCommaParam(httpContext.Request.Query["priority"].ToString());

        var assigneeTypes = ParseCommaParam(httpContext.Request.Query["assignee_types"].ToString());
        var assigneeIdStr = httpContext.Request.Query["assignee_id"].ToString();
        var assigneeIdsStr = httpContext.Request.Query["assignee_ids"].ToString();
        var creatorIdStr = httpContext.Request.Query["creator_id"].ToString();
        var projectIdStr = httpContext.Request.Query["project_id"].ToString();

        // Parse sort
        var sortCol = httpContext.Request.Query["sort"].ToString();
        if (string.IsNullOrEmpty(sortCol)) sortCol = "position";

        var sortDir = httpContext.Request.Query["direction"].ToString().ToUpper();
        if (string.IsNullOrEmpty(sortDir)) sortDir = "ASC";

        // Build query
        var query = db.Issues
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId.Value);

        // Apply filters
        if (statuses.Count > 0)
            query = query.Where(i => statuses.Contains(i.Status));

        if (priorities.Count > 0)
            query = query.Where(i => priorities.Contains(i.Priority));

        if (assigneeTypes.Count > 0)
            query = query.Where(i => i.AssigneeType != null && assigneeTypes.Contains(i.AssigneeType));

        if (Guid.TryParse(assigneeIdStr, out var assigneeId))
            query = query.Where(i => i.AssigneeId == assigneeId);

        if (!string.IsNullOrEmpty(assigneeIdsStr))
        {
            var ids = assigneeIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            if (ids.Count > 0)
                query = query.Where(i => i.AssigneeId.HasValue && ids.Contains(i.AssigneeId.Value));
        }

        if (Guid.TryParse(creatorIdStr, out var creatorId))
            query = query.Where(i => i.CreatorId == creatorId);

        if (Guid.TryParse(projectIdStr, out var projectId))
            query = query.Where(i => i.ProjectId == projectId);

        // Apply sorting
        query = sortCol switch
        {
            "title" => sortDir == "DESC" ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            "created_at" => sortDir == "DESC" ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            "start_date" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.StartDate == null).ThenByDescending(i => i.StartDate)
                : query.OrderBy(i => i.StartDate == null).ThenBy(i => i.StartDate),
            "due_date" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.DueDate == null).ThenByDescending(i => i.DueDate)
                : query.OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate),
            "priority" => sortDir == "DESC"
                ? query.OrderByDescending(i => i.Priority == "urgent" ? 0 : i.Priority == "high" ? 1 : i.Priority == "medium" ? 2 : i.Priority == "low" ? 3 : 4)
                : query.OrderBy(i => i.Priority == "urgent" ? 0 : i.Priority == "high" ? 1 : i.Priority == "medium" ? 2 : i.Priority == "low" ? 3 : 4),
            _ => sortDir == "DESC" ? query.OrderByDescending(i => i.Position) : query.OrderBy(i => i.Position),
        };

        // Materialize and group
        var allIssues = await query.ToListAsync();

        // Get workspace for prefix
        var workspace = await db.Workspaces.FindAsync(workspaceId.Value);
        var prefix = workspace?.IssuePrefix ?? GenerateIssuePrefix(workspace?.Name ?? "");

        // Group by assignee
        var groups = allIssues
            .GroupBy(i => new { i.AssigneeType, i.AssigneeId })
            .Select(g => new IssueAssigneeGroupResponse
            {
                Id = AssigneeGroupId(g.Key.AssigneeType, g.Key.AssigneeId),
                AssigneeType = g.Key.AssigneeType,
                AssigneeId = g.Key.AssigneeId?.ToString(),
                Issues = g.Skip(offset).Take(limit).Select(i => IssueToResponse(i, prefix)).ToList(),
                Total = g.Count()
            })
            .ToList();

        return Results.Ok(new GroupedIssuesResponse { Groups = groups });
    }

    /// <summary>
    /// Generates group ID in format "assignee:{type}:{id}" or "assignee:unassigned".
    /// </summary>
    private static string AssigneeGroupId(string? assigneeType, Guid? assigneeId)
    {
        if (assigneeType == null || assigneeId == null)
            return "assignee:unassigned";
        return $"assignee:{assigneeType}:{assigneeId}";
    }

    /// <summary>
    /// Parses a comma-separated string into a list of trimmed non-empty strings.
    /// </summary>
    private static List<string> ParseCommaParam(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
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
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(issue.Metadata.RootElement.GetRawText()) ?? new Dictionary<string, object>()
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

    public record SearchIssueResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("issue")]
        public IssueResponse Issue { get; init; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("match_source")]
        public string MatchSource { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("matched_snippet")]
        public string? MatchedSnippet { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("matched_description_snippet")]
        public string? MatchedDescriptionSnippet { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("matched_comment_snippet")]
        public string? MatchedCommentSnippet { get; init; }
    }

    public record IssueAssigneeGroupResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("assignee_type")]
        public string? AssigneeType { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("assignee_id")]
        public string? AssigneeId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("issues")]
        public List<IssueResponse> Issues { get; init; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("total")]
        public long Total { get; init; }
    }

    public record GroupedIssuesResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("groups")]
        public List<IssueAssigneeGroupResponse> Groups { get; init; } = new();
    }
}
