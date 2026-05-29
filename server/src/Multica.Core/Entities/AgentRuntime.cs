using System.Text.Json;

namespace Multica.Core.Entities;

public class AgentRuntime
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string? DaemonId { get; set; }
    public string Name { get; set; } = "";
    public string RuntimeMode { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Status { get; set; } = "offline";
    public string DeviceInfo { get; set; } = "";
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? OwnerId { get; set; }
    public string? LegacyDaemonId { get; set; }
    public string Visibility { get; set; } = "private";
}
