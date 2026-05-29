namespace Multica.Core.Auth;

/// <summary>
/// Identity returned for validated task tokens (mat_).
/// </summary>
public record TaskTokenIdentity
{
    public Guid UserId { get; init; }
    public Guid AgentId { get; init; }
    public Guid TaskId { get; init; }
    public Guid WorkspaceId { get; init; }
}
