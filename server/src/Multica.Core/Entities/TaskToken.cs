namespace Multica.Core.Entities;

/// <summary>
/// Task token entity for single-use, short-lived authentication.
/// Compatible with Go's db.TaskToken struct.
/// </summary>
public class TaskToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public Guid AgentId { get; set; }
    public Guid TaskId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
