namespace Multica.Core.Entities;

public class TaskUsageHourly
{
    public DateTimeOffset BucketHour { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid RuntimeId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long CacheWriteTokens { get; set; }
    public long TaskCount { get; set; }
    public long EventCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
