namespace Multica.Core.Entities;

public class PinnedItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public double Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
