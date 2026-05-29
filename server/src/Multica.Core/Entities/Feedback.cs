using System.Text.Json;

namespace Multica.Core.Entities;

public class Feedback
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Message { get; set; } = "";
    public JsonDocument? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
