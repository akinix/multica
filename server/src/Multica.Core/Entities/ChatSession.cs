namespace Multica.Core.Entities;

public class ChatSession
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AgentId { get; set; }
    public Guid CreatorId { get; set; }
    public string Title { get; set; } = "";
    public string? SessionId { get; set; }
    public string? WorkDir { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? UnreadSince { get; set; }
    public Guid? RuntimeId { get; set; }
}
