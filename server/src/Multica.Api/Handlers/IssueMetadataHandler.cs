using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Handlers;

/// <summary>
/// Issue metadata endpoints for KV store operations.
/// Metadata is a JSONB column used by agents to track pipeline state.
/// </summary>
public static partial class IssueMetadataHandler
{
    private const int MaxMetadataKeys = 50;
    private const int MaxMetadataSizeBytes = 8192; // 8KB

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_.-]{0,63}$")]
    private static partial Regex MetadataKeyPattern();

    /// <summary>
    /// Maps issue metadata endpoints to the application.
    /// </summary>
    public static void MapIssueMetadataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/issues");

        group.MapGet("/{id}/metadata", ListIssueMetadata);
        group.MapPut("/{id}/metadata/{key}", SetIssueMetadataKey);
        group.MapDelete("/{id}/metadata/{key}", DeleteIssueMetadataKey);
    }

    /// <summary>
    /// Gets metadata for an issue.
    /// GET /api/issues/{id}/metadata
    /// </summary>
    private static async Task<IResult> ListIssueMetadata(
        string id,
        HttpContext httpContext,
        MulticaDbContext db)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Load issue
        var issue = await LoadIssue(db, id, workspaceId.Value);
        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        var metadata = ParseMetadata(issue.Metadata);
        return Results.Ok(new { metadata });
    }

    /// <summary>
    /// Sets a metadata key on an issue.
    /// PUT /api/issues/{id}/metadata/{key}
    /// </summary>
    private static async Task<IResult> SetIssueMetadataKey(
        string id,
        string key,
        [Microsoft.AspNetCore.Mvc.FromBody] SetMetadataKeyRequest request,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Validate key
        if (string.IsNullOrEmpty(key) || !MetadataKeyPattern().IsMatch(key))
        {
            return Results.BadRequest(new { error = "key must match ^[a-zA-Z_][a-zA-Z0-9_.-]{0,63}$" });
        }

        // Validate value is primitive
        if (request.Value.ValueKind == JsonValueKind.Undefined || request.Value.ValueKind == JsonValueKind.Null)
        {
            return Results.BadRequest(new { error = "value cannot be null (use DELETE to remove a key)" });
        }

        if (!IsPrimitiveValue(request.Value))
        {
            return Results.BadRequest(new { error = "value must be a primitive: string, number, or bool" });
        }

        // Load issue
        var issue = await LoadIssue(db, id, workspaceId.Value);
        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Get existing metadata
        var metadata = ParseMetadata(issue.Metadata);

        // Check key count limit (allow if key already exists)
        if (!metadata.ContainsKey(key) && metadata.Count >= MaxMetadataKeys)
        {
            return Results.BadRequest(new { error = $"metadata cannot exceed {MaxMetadataKeys} keys" });
        }

        // Set the key
        metadata[key] = ConvertJsonValue(request.Value);

        // Serialize and check size
        var serialized = JsonSerializer.Serialize(metadata);
        if (System.Text.Encoding.UTF8.GetByteCount(serialized) > MaxMetadataSizeBytes)
        {
            return Results.BadRequest(new { error = "metadata exceeds the 8KB size limit" });
        }

        // Update issue
        issue.Metadata = JsonDocument.Parse(serialized);
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Metadata key {Key} set on issue {IssueId}", key, issue.Id);

        return Results.Ok(new { metadata });
    }

    /// <summary>
    /// Deletes a metadata key from an issue.
    /// DELETE /api/issues/{id}/metadata/{key}
    /// </summary>
    private static async Task<IResult> DeleteIssueMetadataKey(
        string id,
        string key,
        HttpContext httpContext,
        MulticaDbContext db,
        ILogger<Program> logger)
    {
        var workspaceId = httpContext.Items["WorkspaceId"] as Guid?;
        if (workspaceId is null)
        {
            return Results.BadRequest(new { error = "workspace_id is required" });
        }

        // Validate key
        if (string.IsNullOrEmpty(key) || !MetadataKeyPattern().IsMatch(key))
        {
            return Results.BadRequest(new { error = "key must match ^[a-zA-Z_][a-zA-Z0-9_.-]{0,63}$" });
        }

        // Load issue
        var issue = await LoadIssue(db, id, workspaceId.Value);
        if (issue is null)
        {
            return Results.NotFound(new { error = "issue not found" });
        }

        // Get existing metadata
        var metadata = ParseMetadata(issue.Metadata);

        // Remove the key
        if (!metadata.Remove(key))
        {
            // Key doesn't exist, return current metadata
            return Results.Ok(new { metadata });
        }

        // Serialize updated metadata
        var serialized = JsonSerializer.Serialize(metadata);

        // Update issue
        issue.Metadata = metadata.Count > 0
            ? JsonDocument.Parse(serialized)
            : null;
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Metadata key {Key} deleted from issue {IssueId}", key, issue.Id);

        return Results.Ok(new { metadata });
    }

    /// <summary>
    /// Loads an issue by ID or identifier (e.g., MUL-123).
    /// </summary>
    private static async Task<Issue?> LoadIssue(MulticaDbContext db, string id, Guid workspaceId)
    {
        if (Guid.TryParse(id, out var uuid))
        {
            return await db.Issues
                .FirstOrDefaultAsync(i => i.Id == uuid && i.WorkspaceId == workspaceId);
        }

        var parts = id.Split('-', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
        {
            return null;
        }

        return await db.Issues
            .FirstOrDefaultAsync(i => i.WorkspaceId == workspaceId && i.Number == number);
    }

    /// <summary>
    /// Parses metadata JsonDocument to dictionary. Returns empty dict if null.
    /// </summary>
    private static Dictionary<string, object> ParseMetadata(JsonDocument? doc)
    {
        if (doc is null)
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                doc.RootElement.GetRawText()) ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Validates that a JSON value is a primitive (string, number, bool).
    /// </summary>
    private static bool IsPrimitiveValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => true,
            JsonValueKind.Number => true,
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            _ => false
        };
    }

    /// <summary>
    /// Converts a JsonElement to a CLR object for storage in dictionary.
    /// </summary>
    private static object ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => value.GetRawText()
        };
    }

    // DTOs

    public record SetMetadataKeyRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public JsonElement Value { get; init; }
    }
}
