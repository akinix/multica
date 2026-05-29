namespace Multica.Core.Entities;

public class WorkspaceInvitation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid InviterId { get; set; }
    public string InviteeEmail { get; set; } = "";
    public Guid? InviteeUserId { get; set; }
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
