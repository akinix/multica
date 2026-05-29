namespace Multica.Core.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ChatSessionId { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public Guid? TaskId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? FailureReason { get; set; }
    public long? ElapsedMs { get; set; }
}
