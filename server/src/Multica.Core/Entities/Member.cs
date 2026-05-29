namespace Multica.Core.Entities;

/// <summary>
/// Member entity representing a user's membership in a workspace.
/// Compatible with Go's db.Member struct.
/// </summary>
public class Member
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Role { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public Workspace? Workspace { get; set; }
}
