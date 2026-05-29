using System.Text.Json;

namespace Multica.Core.Entities;

public class AgentTaskQueue
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid? IssueId { get; set; }
    public string Status { get; set; } = "";
    public int Priority { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public JsonDocument? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public JsonDocument? Context { get; set; }
    public Guid RuntimeId { get; set; }
    public string? SessionId { get; set; }
    public string? WorkDir { get; set; }
    public Guid? TriggerCommentId { get; set; }
    public Guid? ChatSessionId { get; set; }
    public Guid? AutopilotRunId { get; set; }
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string? FailureReason { get; set; }
    public string? TriggerSummary { get; set; }
    public bool ForceFreshSession { get; set; }
    public bool IsLeaderTask { get; set; }
    public string? WaitReason { get; set; }
}
