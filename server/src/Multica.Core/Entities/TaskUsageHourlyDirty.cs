namespace Multica.Core.Entities;

public class TaskUsageHourlyDirty
{
    public DateTimeOffset BucketHour { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid RuntimeId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTimeOffset EnqueuedAt { get; set; }
}
