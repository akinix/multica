using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue reaction API endpoints.
/// Compatible with Go's issue_reaction handler implementation.
/// </summary>
public static class IssueReactionHandler
{
    /// <summary>
    /// Maps issue reaction endpoints to the application.
    /// </summary>
    public static void MapIssueReactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues/{id}/reactions");

        group.MapPost("/", AddIssueReaction);
        group.MapDelete("/", RemoveIssueReaction);
    }

    /// <summary>
    /// Adds a reaction to an issue.
    /// POST /api/issues/{id}/reactions
    /// </summary>
    private static async Task<IResult> AddIssueReaction(
        string id,
        [FromBody] EmojiRequest request,
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

        // Validate emoji
        if (string.IsNullOrWhiteSpace(request.Emoji))
        {
            return Results.BadRequest(new { error = "emoji is required" });
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

        // Resolve actor (default to member for now)
        var actorType = "member";
        var actorId = userId;

        // Create reaction
        var reaction = new IssueReaction
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            WorkspaceId = workspaceId.Value,
            ActorType = actorType,
            ActorId = actorId,
            Emoji = request.Emoji,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.IssueReactions.Add(reaction);
        await db.SaveChangesAsync();

        var response = IssueReactionToResponse(reaction);

        logger.LogInformation("Issue reaction added: {ReactionId} on issue {IssueId}", reaction.Id, issue.Id);

        return Results.Created($"/api/issues/{issue.Id}/reactions", response);
    }

    /// <summary>
    /// Removes a reaction from an issue.
    /// DELETE /api/issues/{id}/reactions
    /// </summary>
    private static async Task<IResult> RemoveIssueReaction(
        string id,
        [FromBody] EmojiRequest request,
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

        // Validate emoji
        if (string.IsNullOrWhiteSpace(request.Emoji))
        {
            return Results.BadRequest(new { error = "emoji is required" });
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

        // Resolve actor
        var actorType = "member";
        var actorId = userId;

        // Find and delete reaction
        var reaction = await db.IssueReactions
            .FirstOrDefaultAsync(r =>
                r.IssueId == issue.Id &&
                r.ActorType == actorType &&
                r.ActorId == actorId &&
                r.Emoji == request.Emoji);

        if (reaction is null)
        {
            return Results.NoContent();
        }

        db.IssueReactions.Remove(reaction);
        await db.SaveChangesAsync();

        logger.LogInformation("Issue reaction removed: {Emoji} from issue {IssueId}", request.Emoji, issue.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// Loads reactions for a single issue.
    /// </summary>
    public static async Task<List<IssueReactionResponse>> LoadReactionsForIssue(
        MulticaDbContext db, Guid issueId)
    {
        return await db.IssueReactions
            .AsNoTracking()
            .Where(r => r.IssueId == issueId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new IssueReactionResponse
            {
                Id = r.Id.ToString(),
                IssueId = r.IssueId.ToString(),
                ActorType = r.ActorType,
                ActorId = r.ActorId.ToString(),
                Emoji = r.Emoji,
                CreatedAt = r.CreatedAt.ToString("o")
            })
            .ToListAsync();
    }

    /// <summary>
    /// Bulk loads reactions for multiple issues, grouped by issue_id.
    /// </summary>
    public static async Task<Dictionary<string, List<IssueReactionResponse>>> LoadReactionsForIssues(
        MulticaDbContext db, List<Guid> issueIds)
    {
        if (issueIds.Count == 0)
        {
            return new Dictionary<string, List<IssueReactionResponse>>();
        }

        var reactions = await db.IssueReactions
            .AsNoTracking()
            .Where(r => issueIds.Contains(r.IssueId))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new IssueReactionResponse
            {
                Id = r.Id.ToString(),
                IssueId = r.IssueId.ToString(),
                ActorType = r.ActorType,
                ActorId = r.ActorId.ToString(),
                Emoji = r.Emoji,
                CreatedAt = r.CreatedAt.ToString("o")
            })
            .ToListAsync();

        return reactions
            .GroupBy(r => r.IssueId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Converts an IssueReaction entity to a response DTO.
    /// </summary>
    private static IssueReactionResponse IssueReactionToResponse(IssueReaction reaction)
    {
        return new IssueReactionResponse
        {
            Id = reaction.Id.ToString(),
            IssueId = reaction.IssueId.ToString(),
            ActorType = reaction.ActorType,
            ActorId = reaction.ActorId.ToString(),
            Emoji = reaction.Emoji,
            CreatedAt = reaction.CreatedAt.ToString("o")
        };
    }

    // DTOs

    public record EmojiRequest
    {
        [JsonPropertyName("emoji")]
        public string Emoji { get; init; } = "";
    }

    public record IssueReactionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("issue_id")]
        public string IssueId { get; init; } = "";

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
