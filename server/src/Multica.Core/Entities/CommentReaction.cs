namespace Multica.Core.Entities;

public class CommentReaction
{
    public Guid Id { get; set; }
    public Guid CommentId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string ActorType { get; set; } = "";
    public Guid ActorId { get; set; }
    public string Emoji { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
