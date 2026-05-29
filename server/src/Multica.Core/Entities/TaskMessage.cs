using System.Text.Json;

namespace Multica.Core.Entities;

public class TaskMessage
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public int Seq { get; set; }
    public string Type { get; set; } = "";
    public string? Tool { get; set; }
    public string? Content { get; set; }
    public JsonDocument? Input { get; set; }
    public string? Output { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
