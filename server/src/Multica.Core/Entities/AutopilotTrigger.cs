using System.Text.Json;

namespace Multica.Core.Entities;

public class AutopilotTrigger
{
    public Guid Id { get; set; }
    public Guid AutopilotId { get; set; }
    public string Kind { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string? CronExpression { get; set; }
    public string? Timezone { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public string? WebhookToken { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset? LastFiredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Provider { get; set; } = "generic";
    public string? SigningSecret { get; set; }
    public JsonDocument? EventFilters { get; set; }
}
