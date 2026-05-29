using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Attachment API endpoints for listing issue and comment attachments.
/// Compatible with Go's file handler implementation.
/// </summary>
public static class AttachmentHandler
{
    /// <summary>
    /// Maps attachment-related endpoints to the application.
    /// </summary>
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/issues/{id}/attachments", ListIssueAttachments);
        app.MapGet("/api/comments/{commentId}/attachments", ListCommentAttachments);
    }

    /// <summary>
    /// Lists attachments for an issue.
    /// GET /api/issues/{id}/attachments
    /// </summary>
    private static async Task<IResult> ListIssueAttachments(
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

        // Query attachments for this issue
        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(a => a.IssueId == issue.Id && a.WorkspaceId == workspaceId.Value)
            .OrderBy(a => a.CreatedAt)
            .Select(a => AttachmentToResponse(a))
            .ToListAsync();

        return Results.Ok(attachments);
    }

    /// <summary>
    /// Lists attachments for a comment.
    /// GET /api/comments/{commentId}/attachments
    /// </summary>
    private static async Task<IResult> ListCommentAttachments(
        string commentId,
        HttpContext httpContext,
        MulticaDbContext db)
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

        // Load comment scoped to workspace
        var comment = await db.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentUuid && c.WorkspaceId == workspaceId.Value);

        if (comment is null)
        {
            return Results.NotFound(new { error = "comment not found" });
        }

        // Query attachments for this comment
        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(a => a.CommentId == comment.Id && a.WorkspaceId == workspaceId.Value)
            .OrderBy(a => a.CreatedAt)
            .Select(a => AttachmentToResponse(a))
            .ToListAsync();

        return Results.Ok(attachments);
    }

    /// <summary>
    /// Loads attachments for multiple comment IDs, grouped by comment ID.
    /// Used by ListComments to bulk-load attachments for all comments at once.
    /// </summary>
    public static async Task<Dictionary<string, List<AttachmentResponse>>> LoadAttachmentsForComments(
        MulticaDbContext db,
        List<Guid> commentIds,
        Guid workspaceId)
    {
        if (commentIds.Count == 0)
        {
            return new Dictionary<string, List<AttachmentResponse>>();
        }

        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(a => a.CommentId.HasValue && commentIds.Contains(a.CommentId.Value) && a.WorkspaceId == workspaceId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var grouped = new Dictionary<string, List<AttachmentResponse>>();
        foreach (var a in attachments)
        {
            var cid = a.CommentId!.Value.ToString();
            if (!grouped.ContainsKey(cid))
            {
                grouped[cid] = new List<AttachmentResponse>();
            }
            grouped[cid].Add(AttachmentToResponse(a));
        }

        return grouped;
    }

    /// <summary>
    /// Loads attachments for an issue and returns them as a list.
    /// Used by GetIssue to include attachments in the response.
    /// </summary>
    public static async Task<List<AttachmentResponse>> LoadAttachmentsForIssue(
        MulticaDbContext db,
        Guid issueId,
        Guid workspaceId)
    {
        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(a => a.IssueId == issueId && a.WorkspaceId == workspaceId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => AttachmentToResponse(a))
            .ToListAsync();

        return attachments;
    }

    /// <summary>
    /// Converts an Attachment entity to an AttachmentResponse DTO.
    /// Matches Go's attachmentToResponse format exactly.
    /// </summary>
    private static AttachmentResponse AttachmentToResponse(Attachment a)
    {
        return new AttachmentResponse
        {
            Id = a.Id.ToString(),
            WorkspaceId = a.WorkspaceId.ToString(),
            IssueId = a.IssueId?.ToString(),
            CommentId = a.CommentId?.ToString(),
            ChatSessionId = a.ChatSessionId?.ToString(),
            ChatMessageId = a.ChatMessageId?.ToString(),
            UploaderType = a.UploaderType,
            UploaderId = a.UploaderId.ToString(),
            Filename = a.Filename,
            Url = a.Url,
            DownloadUrl = a.Url, // In Go, DownloadUrl defaults to Url unless CF signer is configured
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes,
            CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz")
        };
    }

    // DTOs

    public record AttachmentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("workspace_id")]
        public string WorkspaceId { get; init; } = "";

        [JsonPropertyName("issue_id")]
        public string? IssueId { get; init; }

        [JsonPropertyName("comment_id")]
        public string? CommentId { get; init; }

        [JsonPropertyName("chat_session_id")]
        public string? ChatSessionId { get; init; }

        [JsonPropertyName("chat_message_id")]
        public string? ChatMessageId { get; init; }

        [JsonPropertyName("uploader_type")]
        public string UploaderType { get; init; } = "";

        [JsonPropertyName("uploader_id")]
        public string UploaderId { get; init; } = "";

        [JsonPropertyName("filename")]
        public string Filename { get; init; } = "";

        [JsonPropertyName("url")]
        public string Url { get; init; } = "";

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; init; } = "";

        [JsonPropertyName("content_type")]
        public string ContentType { get; init; } = "";

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; init; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; init; } = "";
    }
}
