using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue-Pull Request linking API endpoints.
/// Compatible with Go's github handler implementation.
/// </summary>
public static class IssuePullRequestHandler
{
    /// <summary>
    /// Maps issue pull request endpoints to the application.
    /// </summary>
    public static void MapIssuePullRequestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues/{id}/pull-requests");

        group.MapGet("/", ListIssuePullRequests);
        group.MapPost("/", LinkPullRequest);
        group.MapDelete("/{prId}", UnlinkPullRequest);
    }

    /// <summary>
    /// Lists pull requests linked to an issue.
    /// GET /api/issues/{id}/pull-requests
    /// </summary>
    private static async Task<IResult> ListIssuePullRequests(
        string id,
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue by UUID or identifier
        Issue? issue;
        if (Guid.TryParse(id, out var uuid))
        {
            issue = await db.Issues
                .AsNoTracking()
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
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Query issue_pull_requests joined with github_pull_requests
        var pullRequests = await db.IssuePullRequests
            .AsNoTracking()
            .Where(ipr => ipr.IssueId == issue.Id)
            .Join(db.GithubPullRequests,
                ipr => ipr.PullRequestId,
                pr => pr.Id,
                (ipr, pr) => new IssuePullRequestResponse
                {
                    IssueId = ipr.IssueId.ToString(),
                    PullRequestId = ipr.PullRequestId.ToString(),
                    LinkedByType = ipr.LinkedByType,
                    LinkedById = ipr.LinkedById != null ? ipr.LinkedById.ToString() : null,
                    LinkedAt = ipr.LinkedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    CloseIntent = ipr.CloseIntent,
                    // Extended PR fields from github_pull_requests
                    WorkspaceId = pr.WorkspaceId.ToString(),
                    RepoOwner = pr.RepoOwner,
                    RepoName = pr.RepoName,
                    Number = pr.PrNumber,
                    Title = pr.Title,
                    State = pr.State,
                    HtmlUrl = pr.HtmlUrl,
                    Branch = pr.Branch,
                    AuthorLogin = pr.AuthorLogin,
                    AuthorAvatarUrl = pr.AuthorAvatarUrl,
                    MergedAt = pr.MergedAt != null ? pr.MergedAt.Value.ToString("yyyy-MM-ddTHH:mm:sszzz") : null,
                    ClosedAt = pr.ClosedAt != null ? pr.ClosedAt.Value.ToString("yyyy-MM-ddTHH:mm:sszzz") : null,
                    PrCreatedAt = pr.PrCreatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    PrUpdatedAt = pr.PrUpdatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    HeadSha = pr.HeadSha,
                    MergeableState = pr.MergeableState,
                    Additions = pr.Additions,
                    Deletions = pr.Deletions,
                    ChangedFiles = pr.ChangedFiles
                })
            .ToListAsync();

        return Results.Ok(new { pull_requests = pullRequests });
    }

    /// <summary>
    /// Links a pull request to an issue.
    /// POST /api/issues/{id}/pull-requests
    /// </summary>
    private static async Task<IResult> LinkPullRequest(
        string id,
        [FromBody] LinkPullRequestRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Get actor from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Results.Unauthorized();
        }

        // Validate pull_request_id
        if (string.IsNullOrEmpty(request.PullRequestId) || !Guid.TryParse(request.PullRequestId, out var pullRequestId))
        {
            return Results.BadRequest(new { error = "invalid pull_request_id" });
        }

        // Load issue by UUID or identifier
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

        // Verify pull request exists in this workspace
        var prExists = await db.GithubPullRequests
            .AnyAsync(pr => pr.Id == pullRequestId && pr.WorkspaceId == workspaceId.Value);
        if (!prExists)
        {
            return Results.BadRequest(new { error = "pull request not found in this workspace" });
        }

        // Check if link already exists
        var existingLink = await db.IssuePullRequests
            .FirstOrDefaultAsync(ipr => ipr.IssueId == issue.Id && ipr.PullRequestId == pullRequestId);

        if (existingLink is not null)
        {
            // Update close_intent if different (idempotent upsert matching Go's LinkIssueToPullRequest)
            if (existingLink.CloseIntent != request.CloseIntent)
            {
                existingLink.CloseIntent = request.CloseIntent;
                await db.SaveChangesAsync();
            }

            var existingResponse = IssuePullRequestToResponse(existingLink);
            return Results.Ok(existingResponse);
        }

        // Create new link
        var link = new IssuePullRequest
        {
            IssueId = issue.Id,
            PullRequestId = pullRequestId,
            LinkedByType = "member",
            LinkedById = userId,
            LinkedAt = DateTimeOffset.UtcNow,
            CloseIntent = request.CloseIntent
        };

        db.IssuePullRequests.Add(link);
        await db.SaveChangesAsync();

        var response = IssuePullRequestToResponse(link);

        logger.LogInformation("Pull request {PullRequestId} linked to issue {IssueId}", pullRequestId, issue.Id);

        return Results.Created($"/api/issues/{issue.Id}/pull-requests", response);
    }

    /// <summary>
    /// Unlinks a pull request from an issue.
    /// DELETE /api/issues/{id}/pull-requests/{prId}
    /// </summary>
    private static async Task<IResult> UnlinkPullRequest(
        string id,
        string prId,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        if (!Guid.TryParse(prId, out var pullRequestId))
        {
            return Results.BadRequest(new { error = "invalid pull request id" });
        }

        // Load issue by UUID or identifier
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

        // Find and remove the link
        var link = await db.IssuePullRequests
            .FirstOrDefaultAsync(ipr => ipr.IssueId == issue.Id && ipr.PullRequestId == pullRequestId);

        if (link is null)
        {
            return Results.NotFound(new { error = "pull request link not found" });
        }

        db.IssuePullRequests.Remove(link);
        await db.SaveChangesAsync();

        logger.LogInformation("Pull request {PullRequestId} unlinked from issue {IssueId}", pullRequestId, issue.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// Converts an IssuePullRequest entity to a response DTO.
    /// </summary>
    private static IssuePullRequestResponse IssuePullRequestToResponse(IssuePullRequest ipr)
    {
        return new IssuePullRequestResponse
        {
            IssueId = ipr.IssueId.ToString(),
            PullRequestId = ipr.PullRequestId.ToString(),
            LinkedByType = ipr.LinkedByType,
            LinkedById = ipr.LinkedById?.ToString(),
            LinkedAt = ipr.LinkedAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            CloseIntent = ipr.CloseIntent
        };
    }

    // DTOs

    public record LinkPullRequestRequest
    {
        [JsonPropertyName("pull_request_id")]
        public string PullRequestId { get; init; } = "";

        [JsonPropertyName("close_intent")]
        public bool CloseIntent { get; init; }
    }

    public record IssuePullRequestResponse
    {
        [JsonPropertyName("issue_id")]
        public string IssueId { get; init; } = "";

        [JsonPropertyName("pull_request_id")]
        public string PullRequestId { get; init; } = "";

        [JsonPropertyName("linked_by_type")]
        public string? LinkedByType { get; init; }

        [JsonPropertyName("linked_by_id")]
        public string? LinkedById { get; init; }

        [JsonPropertyName("linked_at")]
        public string LinkedAt { get; init; } = "";

        [JsonPropertyName("close_intent")]
        public bool CloseIntent { get; init; }

        // Extended PR fields from github_pull_requests (for list endpoint)
        [JsonPropertyName("workspace_id")]
        public string? WorkspaceId { get; init; }

        [JsonPropertyName("repo_owner")]
        public string? RepoOwner { get; init; }

        [JsonPropertyName("repo_name")]
        public string? RepoName { get; init; }

        [JsonPropertyName("number")]
        public int? Number { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("branch")]
        public string? Branch { get; init; }

        [JsonPropertyName("author_login")]
        public string? AuthorLogin { get; init; }

        [JsonPropertyName("author_avatar_url")]
        public string? AuthorAvatarUrl { get; init; }

        [JsonPropertyName("merged_at")]
        public string? MergedAt { get; init; }

        [JsonPropertyName("closed_at")]
        public string? ClosedAt { get; init; }

        [JsonPropertyName("pr_created_at")]
        public string? PrCreatedAt { get; init; }

        [JsonPropertyName("pr_updated_at")]
        public string? PrUpdatedAt { get; init; }

        [JsonPropertyName("head_sha")]
        public string? HeadSha { get; init; }

        [JsonPropertyName("mergeable_state")]
        public string? MergeableState { get; init; }

        [JsonPropertyName("additions")]
        public int? Additions { get; init; }

        [JsonPropertyName("deletions")]
        public int? Deletions { get; init; }

        [JsonPropertyName("changed_files")]
        public int? ChangedFiles { get; init; }
    }
}
