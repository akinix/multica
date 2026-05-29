using System.Text.Json;

namespace Multica.Core.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public JsonDocument Preferences { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset UpdatedAt { get; set; }
}
