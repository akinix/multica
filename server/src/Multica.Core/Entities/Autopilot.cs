using System.Text.Json;

namespace Multica.Core.Entities;

public class Autopilot
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid AssigneeId { get; set; }
    public string Status { get; set; } = "";
    public string ExecutionMode { get; set; } = "";
    public string? IssueTitleTemplate { get; set; }
    public string CreatedByType { get; set; } = "";
    public Guid CreatedById { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string AssigneeType { get; set; } = "";
    public Guid? ProjectId { get; set; }
}
