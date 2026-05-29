using System.Text.Json;

namespace Multica.Core.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? IssueId { get; set; }
    public string? ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public JsonDocument Details { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset CreatedAt { get; set; }
}
