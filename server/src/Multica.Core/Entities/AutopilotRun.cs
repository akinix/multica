using System.Text.Json;

namespace Multica.Core.Entities;

public class AutopilotRun
{
    public Guid Id { get; set; }
    public Guid AutopilotId { get; set; }
    public Guid? TriggerId { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
    public Guid? IssueId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public JsonDocument? TriggerPayload { get; set; }
    public JsonDocument? Result { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SquadId { get; set; }
}
