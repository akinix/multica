using System.Text.Json;

namespace Multica.Core.Entities;

public class DaemonConnection
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string DaemonId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public JsonDocument RuntimeInfo { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
