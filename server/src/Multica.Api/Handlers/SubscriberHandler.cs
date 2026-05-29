using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue subscription API endpoints.
/// Compatible with Go's subscriber handler implementation.
/// </summary>
public static class SubscriberHandler
{
    /// <summary>
    /// Maps subscriber-related endpoints to the application.
    /// </summary>
    public static void MapSubscriberEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues/{id}");

        group.MapGet("/subscribers", ListIssueSubscribers);
        group.MapPost("/subscribe", SubscribeToIssue);
        group.MapPost("/unsubscribe", UnsubscribeFromIssue);
    }

    /// <summary>
    /// Lists all subscribers for an issue.
    /// GET /api/issues/{id}/subscribers
    /// </summary>
    private static async Task<IResult> ListIssueSubscribers(
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
        if (Guid.TryParse(id, out var issueUuid))
        {
            issue = await db.Issues
                .AsNoTracking()
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
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.WorkspaceId == workspaceId.Value &&
                    i.Number == number);
        }

        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Query subscribers for issue
        var subscribers = await db.IssueSubscribers
            .AsNoTracking()
            .Where(s => s.IssueId == issue.Id)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new SubscriberResponse
            {
                IssueId = s.IssueId.ToString(),
                UserType = s.UserType,
                UserId = s.UserId.ToString(),
                Reason = s.Reason,
                CreatedAt = s.CreatedAt.ToString("o")
            })
            .ToListAsync();

        return Results.Ok(subscribers);
    }

    /// <summary>
    /// Subscribes a user to an issue with reason "manual".
    /// If request body contains user_id, subscribes that user; otherwise subscribes the caller.
    /// POST /api/issues/{id}/subscribe
    /// </summary>
    private static async Task<IResult> SubscribeToIssue(
        string id,
        [FromBody] SubscribeRequest? request,
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

        // Get caller from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var callerId))
        {
            return Results.Unauthorized();
        }

        // Default target: the caller
        var targetUserType = "member";
        var targetUserId = callerId;

        // If request body provides user_id, use that instead
        if (request is not null)
        {
            if (!string.IsNullOrEmpty(request.UserId) && Guid.TryParse(request.UserId, out var parsedUserId))
            {
                targetUserId = parsedUserId;
            }
            if (!string.IsNullOrEmpty(request.UserType))
            {
                targetUserType = request.UserType;
            }
        }

        // Check if already subscribed
        var existingSubscriber = await db.IssueSubscribers
            .FirstOrDefaultAsync(s => s.IssueId == issue.Id && s.UserType == targetUserType && s.UserId == targetUserId);

        if (existingSubscriber is not null)
        {
            // Already subscribed, return success
            return Results.Ok(new { subscribed = true });
        }

        // Create subscriber
        var subscriber = new IssueSubscriber
        {
            IssueId = issue.Id,
            UserType = targetUserType,
            UserId = targetUserId,
            Reason = "manual",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.IssueSubscribers.Add(subscriber);
        await db.SaveChangesAsync();

        return Results.Ok(new { subscribed = true });
    }

    /// <summary>
    /// Unsubscribes a user from an issue.
    /// If request body contains user_id, unsubscribes that user; otherwise unsubscribes the caller.
    /// POST /api/issues/{id}/unsubscribe
    /// </summary>
    private static async Task<IResult> UnsubscribeFromIssue(
        string id,
        [FromBody] SubscribeRequest? request,
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

        // Get caller from auth context
        var userIdStr = httpContext.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var callerId))
        {
            return Results.Unauthorized();
        }

        // Default target: the caller
        var targetUserType = "member";
        var targetUserId = callerId;

        // If request body provides user_id, use that instead
        if (request is not null)
        {
            if (!string.IsNullOrEmpty(request.UserId) && Guid.TryParse(request.UserId, out var parsedUserId))
            {
                targetUserId = parsedUserId;
            }
            if (!string.IsNullOrEmpty(request.UserType))
            {
                targetUserType = request.UserType;
            }
        }

        // Find and remove subscriber
        var subscriber = await db.IssueSubscribers
            .FirstOrDefaultAsync(s => s.IssueId == issue.Id && s.UserType == targetUserType && s.UserId == targetUserId);

        if (subscriber is not null)
        {
            db.IssueSubscribers.Remove(subscriber);
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { subscribed = false });
    }

    // DTOs

    /// <summary>
    /// Response DTO for issue subscriber.
    /// </summary>
    public record SubscriberResponse
    {
        [JsonPropertyName("issue_id")]
        public string IssueId { get; init; } = "";

        [JsonPropertyName("user_type")]
        public string UserType { get; init; } = "";

        [JsonPropertyName("user_id")]
        public string UserId { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; init; } = "";
    }

    /// <summary>
    /// Request DTO for subscribe/unsubscribe operations.
    /// </summary>
    public record SubscribeRequest
    {
        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        [JsonPropertyName("user_type")]
        public string? UserType { get; init; }
    }
}
