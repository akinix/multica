using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Comment reaction API endpoints.
/// Compatible with Go's reaction handler implementation.
/// </summary>
public static class ReactionHandler
{
    /// <summary>
    /// Maps comment reaction endpoints to the application.
    /// </summary>
    public static void MapReactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comments/{commentId}/reactions");

        group.MapPost("/", AddReaction);
        group.MapDelete("/", RemoveReaction);
    }

    /// <summary>
    /// Adds a reaction to a comment.
    /// POST /api/comments/{commentId}/reactions
    /// </summary>
    private static async Task<IResult> AddReaction(
        string commentId,
        [FromBody] ReactionEmojiRequest request,
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

        // Validate comment ID
        if (!Guid.TryParse(commentId, out var commentUuid))
        {
            return Results.BadRequest(new { error = "invalid comment id" });
        }

        // Validate emoji
        if (string.IsNullOrWhiteSpace(request.Emoji))
        {
            return Results.BadRequest(new { error = "emoji is required" });
        }

        // Load comment scoped to workspace
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);
        if (comment is null)
        {
            return Results.NotFound(new { error = "comment not found" });
        }

        // Resolve actor
        var actorType = "member";
        var actorId = userId;

        // Create reaction
        var reaction = new CommentReaction
        {
            Id = Guid.NewGuid(),
            CommentId = comment.Id,
            WorkspaceId = workspaceId.Value,
            ActorType = actorType,
            ActorId = actorId,
            Emoji = request.Emoji,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.CommentReactions.Add(reaction);
        await db.SaveChangesAsync();

        var response = ReactionToResponse(reaction);

        logger.LogInformation("Comment reaction added: {ReactionId} on comment {CommentId}", reaction.Id, comment.Id);

        return Results.Created($"/api/comments/{comment.Id}/reactions", response);
    }

    /// <summary>
    /// Removes a reaction from a comment.
    /// DELETE /api/comments/{commentId}/reactions
    /// </summary>
    private static async Task<IResult> RemoveReaction(
        string commentId,
        [FromBody] ReactionEmojiRequest request,
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

        // Validate comment ID
        if (!Guid.TryParse(commentId, out var commentUuid))
        {
            return Results.BadRequest(new { error = "invalid comment id" });
        }

        // Validate emoji
        if (string.IsNullOrWhiteSpace(request.Emoji))
        {
            return Results.BadRequest(new { error = "emoji is required" });
        }

        // Load comment scoped to workspace
        var comment = await db.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);
        if (comment is null)
        {
            return Results.NotFound(new { error = "comment not found" });
        }

        // Resolve actor
        var actorType = "member";
        var actorId = userId;

        // Find and delete reaction
        var reaction = await db.CommentReactions
            .FirstOrDefaultAsync(r =>
                r.CommentId == comment.Id &&
                r.ActorType == actorType &&
                r.ActorId == actorId &&
                r.Emoji == request.Emoji);

        if (reaction is null)
        {
            return Results.NoContent();
        }

        db.CommentReactions.Remove(reaction);
        await db.SaveChangesAsync();

        logger.LogInformation("Comment reaction removed: {Emoji} from comment {CommentId}", request.Emoji, comment.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// Loads reactions for a single comment.
    /// </summary>
    public static async Task<List<ReactionResponse>> LoadReactionsForComment(
        MulticaDbContext db, Guid commentId)
    {
        return await db.CommentReactions
            .AsNoTracking()
            .Where(r => r.CommentId == commentId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReactionResponse
            {
                Id = r.Id.ToString(),
                CommentId = r.CommentId.ToString(),
                ActorType = r.ActorType,
                ActorId = r.ActorId.ToString(),
                Emoji = r.Emoji,
                CreatedAt = r.CreatedAt.ToString("o")
            })
            .ToListAsync();
    }

    /// <summary>
    /// Bulk loads reactions for multiple comments, grouped by comment_id.
    /// </summary>
    public static async Task<Dictionary<string, List<ReactionResponse>>> LoadReactionsForComments(
        MulticaDbContext db, List<Guid> commentIds)
    {
        if (commentIds.Count == 0)
        {
            return new Dictionary<string, List<ReactionResponse>>();
        }

        var reactions = await db.CommentReactions
            .AsNoTracking()
            .Where(r => commentIds.Contains(r.CommentId))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReactionResponse
            {
                Id = r.Id.ToString(),
                CommentId = r.CommentId.ToString(),
                ActorType = r.ActorType,
                ActorId = r.ActorId.ToString(),
                Emoji = r.Emoji,
                CreatedAt = r.CreatedAt.ToString("o")
            })
            .ToListAsync();

        return reactions
            .GroupBy(r => r.CommentId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Converts a CommentReaction entity to a response DTO.
    /// </summary>
    private static ReactionResponse ReactionToResponse(CommentReaction reaction)
    {
        return new ReactionResponse
        {
            Id = reaction.Id.ToString(),
            CommentId = reaction.CommentId.ToString(),
            ActorType = reaction.ActorType,
            ActorId = reaction.ActorId.ToString(),
            Emoji = reaction.Emoji,
            CreatedAt = reaction.CreatedAt.ToString("o")
        };
    }

    // DTOs

    public record ReactionEmojiRequest
    {
        [JsonPropertyName("emoji")]
        public string Emoji { get; init; } = "";
    }

    public record ReactionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("comment_id")]
        public string CommentId { get; init; } = "";

        [JsonPropertyName("actor_type")]
        public string ActorType { get; init; } = "";

        [JsonPropertyName("actor_id")]
        public string ActorId { get; init; } = "";

        [JsonPropertyName("emoji")]
        public string Emoji { get; init; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; init; } = "";
    }
}
