using System.Text.Json;

namespace Multica.Core.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string RuntimeMode { get; set; } = "";
    public JsonDocument RuntimeConfig { get; set; } = JsonDocument.Parse("{}");
    public string Visibility { get; set; } = "";
    public string Status { get; set; } = "";
    public int MaxConcurrentTasks { get; set; } = 1;
    public Guid? OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Description { get; set; } = "";
    public Guid RuntimeId { get; set; }
    public string Instructions { get; set; } = "";
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedBy { get; set; }
    public JsonDocument? CustomEnv { get; set; }
    public JsonDocument? CustomArgs { get; set; }
    public JsonDocument? McpConfig { get; set; }
    public string? Model { get; set; }
    public string? ThinkingLevel { get; set; }
}
