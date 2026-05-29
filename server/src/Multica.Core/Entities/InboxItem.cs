using System.Text.Json;

namespace Multica.Core.Entities;

public class InboxItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string RecipientType { get; set; } = "";
    public Guid RecipientId { get; set; }
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public Guid? IssueId { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public bool Read { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public JsonDocument? Details { get; set; }
}
