namespace Multica.Core.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string AuthorType { get; set; } = "";
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? ParentId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedByType { get; set; }
    public Guid? ResolvedById { get; set; }
}
