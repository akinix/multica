using System.Text.Json;

namespace Multica.Core.Entities;

public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AutopilotId { get; set; }
    public Guid TriggerId { get; set; }
    public string Provider { get; set; } = "";
    public string Event { get; set; } = "";
    public string? DedupeKey { get; set; }
    public string? DedupeSource { get; set; }
    public string SignatureStatus { get; set; } = "";
    public string Status { get; set; } = "";
    public int AttemptCount { get; set; } = 1;
    public JsonDocument SelectedHeaders { get; set; } = JsonDocument.Parse("{}");
    public string? ContentType { get; set; }
    public byte[]? RawBody { get; set; }
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public Guid? AutopilotRunId { get; set; }
    public Guid? ReplayedFromDeliveryId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
