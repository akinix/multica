namespace Multica.Core.Entities;

/// <summary>
/// Daemon token entity for daemon-specific authentication.
/// Compatible with Go's db.DaemonToken struct.
/// </summary>
public class DaemonToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid WorkspaceId { get; set; }
    public string DaemonId { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
