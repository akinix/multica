using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;
using static Multica.Api.Handlers.AttachmentHandler;
using static Multica.Api.Handlers.ReactionHandler;

namespace Multica.Api.Handlers;

/// <summary>
/// Comment API endpoints for CRUD operations.
/// Compatible with Go's comment handler implementation.
/// </summary>
public static class CommentHandler
{
    /// <summary>
    /// Hard cap on comments returned per issue, matching Go's commentHardCap.
    /// </summary>
    private const int CommentHardCap = 2000;

    /// <summary>
    /// Maps comment-related endpoints to the application.
    /// </summary>
    public static void MapCommentEndpoints(this WebApplication app)
    {
        // Issue-scoped comment endpoints
        var issueComments = app.MapGroup("/api/issues/{issueId}/comments");
        issueComments.MapGet("/", ListComments);
        issueComments.MapGet("/timeline", ListCommentTimeline);
        issueComments.MapPost("/", CreateComment);

        // Comment-scoped endpoints
        var comments = app.MapGroup("/api/comments/{commentId}");
        comments.MapPatch("/", UpdateComment);
        comments.MapDelete("/", DeleteComment);
        comments.MapPost("/resolve", ResolveComment);
        comments.MapPost("/unresolve", UnresolveComment);
    }

    /// <summary>
    /// Lists comments for an issue in chronological order.
    /// </summary>
    private static async Task<IResult> ListComments(
        string issueId,
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
        if (Guid.TryParse(issueId, out var issueUuid))
        {
            issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = issueId.Split('-', 2);
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

        // Query comments for issue, ordered by CreatedAt ASC, capped at 2000
        var comments = await db.Comments
            .AsNoTracking()
            .Where(c => c.IssueId == issue.Id)
            .OrderBy(c => c.CreatedAt)
            .Take(CommentHardCap)
            .Select(c => new CommentResponse
            {
                Id = c.Id.ToString(),
                IssueId = c.IssueId.ToString(),
                AuthorType = c.AuthorType,
                AuthorId = c.AuthorId.ToString(),
                Content = c.Content,
                Type = c.Type,
                ParentId = c.ParentId != null ? c.ParentId.ToString() : null,
                CreatedAt = c.CreatedAt.ToString("o"),
                UpdatedAt = c.UpdatedAt.ToString("o"),
                ResolvedAt = c.ResolvedAt != null ? c.ResolvedAt.Value.ToString("o") : null,
                ResolvedByType = c.ResolvedByType,
                ResolvedById = c.ResolvedById != null ? c.ResolvedById.ToString() : null,
                Reactions = new List<ReactionResponse>(),
                Attachments = new List<AttachmentResponse>()
            })
            .ToListAsync();

        // Bulk-load attachments for all comments at once
        var commentIds = comments.Select(c => Guid.Parse(c.Id)).ToList();
        var attachmentsMap = await AttachmentHandler.LoadAttachmentsForComments(db, commentIds, workspaceId.Value);

        // Bulk-load reactions for all comments at once
        var reactionsMap = await LoadReactionsForComments(db, commentIds);

        // Attach attachments and reactions to each comment
        foreach (var comment in comments)
        {
            if (attachmentsMap.TryGetValue(comment.Id, out var attachments))
            {
                comment.Attachments = attachments;
            }
            if (reactionsMap.TryGetValue(comment.Id, out var reactions))
            {
                comment.Reactions = reactions;
            }
        }

        return Results.Ok(comments);
    }

    /// <summary>
    /// Creates a new comment on an issue.
    /// </summary>
    private static async Task<IResult> CreateComment(
        string issueId,
        [FromBody] CreateCommentRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Get author from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var authorId))
        {
            return Results.Unauthorized();
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { error = "content is required" });
        }

        // Load issue by UUID or identifier
        Issue? issue;
        if (Guid.TryParse(issueId, out var issueUuid))
        {
            issue = await db.Issues
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = issueId.Split('-', 2);
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

        // Default type to "comment" if not provided
        var commentType = string.IsNullOrEmpty(request.Type) ? "comment" : request.Type;

        // Validate parent comment if provided
        Guid? parentId = null;
        if (!string.IsNullOrEmpty(request.ParentId))
        {
            if (!Guid.TryParse(request.ParentId, out var parentUuid))
            {
                return Results.BadRequest(new { error = "invalid parent_id" });
            }

            var parentComment = await db.Comments
                .FirstOrDefaultAsync(c => c.Id == parentUuid && c.IssueId == issue.Id);
            if (parentComment is null)
            {
                return Results.BadRequest(new { error = "invalid parent comment" });
            }
            parentId = parentUuid;
        }

        // Create comment entity
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            AuthorType = "member",
            AuthorId = authorId,
            Content = request.Content,
            Type = commentType,
            ParentId = parentId,
            WorkspaceId = workspaceId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var response = CommentToResponse(comment);

        logger.LogInformation("Comment created: {CommentId} on issue {IssueId}", comment.Id, issue.Id);

        return Results.Created($"/api/comments/{comment.Id}", response);
    }

    /// <summary>
    /// Updates a comment (author or admin only).
    /// </summary>
    private static async Task<IResult> UpdateComment(
        string commentId,
        [FromBody] UpdateCommentRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        if (!Guid.TryParse(commentId, out var commentUuid))
        {
            return Results.BadRequest(new { error = "invalid comment id" });
        }

        // Get author from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Results.Unauthorized();
        }

        // Load comment scoped to workspace
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);
        if (comment is null)
        {
            return Results.NotFound(new { error = "comment not found" });
        }

        // Check authorization: caller must be author or workspace admin/owner
        var isAuthor = comment.AuthorType == "member" && comment.AuthorId == userId;
        if (!isAuthor)
        {
            // Check if user is workspace admin/owner
            var member = await db.Members
                .FirstOrDefaultAsync(m => m.UserId == userId && m.WorkspaceId == workspaceId.Value);
            if (member is null || (member.Role != "owner" && member.Role != "admin"))
            {
                return Results.StatusCode(403);
            }
        }

        // Validate content
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { error = "content is required" });
        }

        // Update comment
        comment.Content = request.Content;
        comment.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        var response = CommentToResponse(comment);

        logger.LogInformation("Comment updated: {CommentId}", comment.Id);

        return Results.Ok(response);
    }

    /// <summary>
    /// Deletes a comment (author or admin only).
    /// </summary>
    private static async Task<IResult> DeleteComment(
        string commentId,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        if (!Guid.TryParse(commentId, out var commentUuid))
        {
            return Results.BadRequest(new { error = "invalid comment id" });
        }

        // Get author from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Results.Unauthorized();
        }

        // Load comment scoped to workspace
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);
        if (comment is null)
        {
            return Results.NotFound(new { error = "comment not found" });
        }

        // Check authorization: caller must be author or workspace admin/owner
        var isAuthor = comment.AuthorType == "member" && comment.AuthorId == userId;
        if (!isAuthor)
        {
            // Check if user is workspace admin/owner
            var member = await db.Members
                .FirstOrDefaultAsync(m => m.UserId == userId && m.WorkspaceId == workspaceId.Value);
            if (member is null || (member.Role != "owner" && member.Role != "admin"))
            {
                return Results.StatusCode(403);
            }
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();

        logger.LogInformation("Comment deleted: {CommentId} on issue {IssueId}", comment.Id, comment.IssueId);

        return Results.NoContent();
    }

    /// <summary>
    /// Resolves a root comment thread.
    /// Only root comments (ParentId == null) can be resolved.
    /// </summary>
    private static async Task<IResult> ResolveComment(
        string commentId,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var (comment, _, actorType, actorId, error) = await LoadRootCommentForActor(commentId, httpContext, db);
        if (error is not null) return error;

        // No-op if already resolved
        if (comment!.ResolvedAt.HasValue)
        {
            var existing = CommentToResponse(comment);
            // Load reactions and attachments for response
            await EnrichCommentResponse(db, existing, comment.WorkspaceId);
            return Results.Ok(existing);
        }

        comment.ResolvedAt = DateTimeOffset.UtcNow;
        comment.ResolvedByType = actorType;
        comment.ResolvedById = actorId;
        await db.SaveChangesAsync();

        var response = CommentToResponse(comment);
        await EnrichCommentResponse(db, response, comment.WorkspaceId);

        logger.LogInformation("Comment resolved: {CommentId}", comment.Id);
        return Results.Ok(response);
    }

    /// <summary>
    /// Unresolves a root comment thread.
    /// Only root comments (ParentId == null) can be unresolved.
    /// </summary>
    private static async Task<IResult> UnresolveComment(
        string commentId,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var (comment, _, _, _, error) = await LoadRootCommentForActor(commentId, httpContext, db);
        if (error is not null) return error;

        // No-op if already unresolved
        if (!comment!.ResolvedAt.HasValue)
        {
            var existing = CommentToResponse(comment);
            await EnrichCommentResponse(db, existing, comment.WorkspaceId);
            return Results.Ok(existing);
        }

        comment.ResolvedAt = null;
        comment.ResolvedByType = null;
        comment.ResolvedById = null;
        await db.SaveChangesAsync();

        var response = CommentToResponse(comment);
        await EnrichCommentResponse(db, response, comment.WorkspaceId);

        logger.LogInformation("Comment unresolved: {CommentId}", comment.Id);
        return Results.Ok(response);
    }

    /// <summary>
    /// Lists comment timeline for an issue (chronological, ASC).
    /// Per D-16: Returns pure comment list sorted by time, same as ListComments.
    /// </summary>
    private static async Task<IResult> ListCommentTimeline(
        string issueId,
        [FromQuery] string? since,
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
        if (Guid.TryParse(issueId, out var issueUuid))
        {
            issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == issueUuid && i.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var parts = issueId.Split('-', 2);
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

        // Parse since parameter (RFC3339 format)
        DateTimeOffset? sinceTime = null;
        if (!string.IsNullOrEmpty(since))
        {
            if (DateTimeOffset.TryParse(since, out var parsed))
            {
                sinceTime = parsed;
            }
            else
            {
                return Results.BadRequest(new { error = "invalid since parameter; expected RFC3339 format" });
            }
        }

        // Query comments for issue, ordered by CreatedAt ASC, capped at 2000
        var query = db.Comments
            .AsNoTracking()
            .Where(c => c.IssueId == issue.Id);

        if (sinceTime.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= sinceTime.Value);
        }

        var comments = await query
            .OrderBy(c => c.CreatedAt)
            .Take(CommentHardCap)
            .Select(c => new CommentResponse
            {
                Id = c.Id.ToString(),
                IssueId = c.IssueId.ToString(),
                AuthorType = c.AuthorType,
                AuthorId = c.AuthorId.ToString(),
                Content = c.Content,
                Type = c.Type,
                ParentId = c.ParentId != null ? c.ParentId.ToString() : null,
                CreatedAt = c.CreatedAt.ToString("o"),
                UpdatedAt = c.UpdatedAt.ToString("o"),
                ResolvedAt = c.ResolvedAt != null ? c.ResolvedAt.Value.ToString("o") : null,
                ResolvedByType = c.ResolvedByType,
                ResolvedById = c.ResolvedById != null ? c.ResolvedById.ToString() : null,
                Reactions = new List<ReactionResponse>(),
                Attachments = new List<AttachmentResponse>()
            })
            .ToListAsync();

        // Bulk-load attachments and reactions
        var commentIds = comments.Select(c => Guid.Parse(c.Id)).ToList();
        var attachmentsMap = await AttachmentHandler.LoadAttachmentsForComments(db, commentIds, workspaceId.Value);
        var reactionsMap = await LoadReactionsForComments(db, commentIds);

        foreach (var comment in comments)
        {
            if (attachmentsMap.TryGetValue(comment.Id, out var attachments))
            {
                comment.Attachments = attachments;
            }
            if (reactionsMap.TryGetValue(comment.Id, out var reactions))
            {
                comment.Reactions = reactions;
            }
        }

        return Results.Ok(comments);
    }

    /// <summary>
    /// Helper: Loads a root comment for the current actor.
    /// Validates workspace access and that the comment is a root comment (no parent).
    /// Returns the comment, workspaceId, actorType, actorId, and an error result if validation fails.
    /// </summary>
    private static async Task<(Comment? comment, Guid workspaceId, string actorType, Guid actorId, IResult? error)> LoadRootCommentForActor(
        string commentId,
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return (null, Guid.Empty, "", Guid.Empty, Results.BadRequest(new { error = "workspace_id is required" }));
        }

        if (!Guid.TryParse(commentId, out var commentUuid))
        {
            return (null, Guid.Empty, "", Guid.Empty, Results.BadRequest(new { error = "invalid comment id" }));
        }

        // Get user ID from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return (null, Guid.Empty, "", Guid.Empty, Results.Unauthorized());
        }

        // Load comment scoped to workspace
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);
        if (comment is null)
        {
            return (null, Guid.Empty, "", Guid.Empty, Results.NotFound(new { error = "comment not found" }));
        }

        // Validate is root comment (no parent)
        if (comment.ParentId.HasValue)
        {
            return (null, Guid.Empty, "", Guid.Empty, Results.BadRequest(new { error = "only root comments can be resolved" }));
        }

        // Resolve actor type (member vs agent)
        var actorType = "member";
        var actorId = userId;
        var actorSource = httpContext.Request.Headers["X-Actor-Source"].ToString();
        if (actorSource == "task_token")
        {
            var agentIdStr = httpContext.Request.Headers["X-Agent-ID"].ToString();
            if (!string.IsNullOrEmpty(agentIdStr) && Guid.TryParse(agentIdStr, out var agentId))
            {
                actorType = "agent";
                actorId = agentId;
            }
        }

        return (comment, workspaceId.Value, actorType, actorId, null);
    }

    /// <summary>
    /// Enriches a CommentResponse with reactions and attachments.
    /// </summary>
    private static async Task EnrichCommentResponse(MulticaDbContext db, CommentResponse response, Guid workspaceId)
    {
        var commentId = Guid.Parse(response.Id);
        var attachmentsMap = await AttachmentHandler.LoadAttachmentsForComments(db, new List<Guid> { commentId }, workspaceId);
        var reactionsMap = await LoadReactionsForComments(db, new List<Guid> { commentId });

        if (attachmentsMap.TryGetValue(response.Id, out var attachments))
        {
            response.Attachments = attachments;
        }
        if (reactionsMap.TryGetValue(response.Id, out var reactions))
        {
            response.Reactions = reactions;
        }
    }

    /// <summary>
    /// Converts a Comment entity to a CommentResponse DTO.
    /// </summary>
    private static CommentResponse CommentToResponse(Comment comment)
    {
        return new CommentResponse
        {
            Id = comment.Id.ToString(),
            IssueId = comment.IssueId.ToString(),
            AuthorType = comment.AuthorType,
            AuthorId = comment.AuthorId.ToString(),
            Content = comment.Content,
            Type = comment.Type,
            ParentId = comment.ParentId?.ToString(),
            CreatedAt = comment.CreatedAt.ToString("o"),
            UpdatedAt = comment.UpdatedAt.ToString("o"),
            ResolvedAt = comment.ResolvedAt?.ToString("o"),
            ResolvedByType = comment.ResolvedByType,
            ResolvedById = comment.ResolvedById?.ToString(),
            Reactions = new List<ReactionResponse>(),
            Attachments = new List<AttachmentResponse>()
        };
    }

    // DTOs

    public record CreateCommentRequest
    {
        public string Content { get; init; } = "";
        public string? Type { get; init; }
        public string? ParentId { get; init; }
        public List<string>? AttachmentIds { get; init; }
    }

    public record UpdateCommentRequest
    {
        public string Content { get; init; } = "";
    }

    public record CommentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("issue_id")]
        public string IssueId { get; init; } = "";

        [JsonPropertyName("author_type")]
        public string AuthorType { get; init; } = "";

        [JsonPropertyName("author_id")]
        public string AuthorId { get; init; } = "";

        [JsonPropertyName("content")]
        public string Content { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("parent_id")]
        public string? ParentId { get; init; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; init; } = "";

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; init; } = "";

        [JsonPropertyName("resolved_at")]
        public string? ResolvedAt { get; init; }

        [JsonPropertyName("resolved_by_type")]
        public string? ResolvedByType { get; init; }

        [JsonPropertyName("resolved_by_id")]
        public string? ResolvedById { get; init; }

        [JsonPropertyName("reactions")]
        public List<ReactionResponse> Reactions { get; set; } = new();

        [JsonPropertyName("attachments")]
        public List<AttachmentResponse> Attachments { get; set; } = new();
    }
}
