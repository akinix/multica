namespace Multica.Core.Entities;

public class IssueReaction
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string ActorType { get; set; } = "";
    public Guid ActorId { get; set; }
    public string Emoji { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
