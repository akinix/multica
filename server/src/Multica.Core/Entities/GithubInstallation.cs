namespace Multica.Core.Entities;

public class GithubInstallation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public long InstallationId { get; set; }
    public string AccountLogin { get; set; } = "";
    public string AccountType { get; set; } = "";
    public string? AccountAvatarUrl { get; set; }
    public Guid? ConnectedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
