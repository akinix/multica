namespace Multica.Core.Entities;

public class TaskUsageHourlyRollupState
{
    public short Id { get; set; }
    public DateTimeOffset WatermarkAt { get; set; }
    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunFinishedAt { get; set; }
    public long LastRunRows { get; set; }
    public string? LastError { get; set; }
}
