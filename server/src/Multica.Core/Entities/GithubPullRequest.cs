namespace Multica.Core.Entities;

public class GithubPullRequest
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public long InstallationId { get; set; }
    public string RepoOwner { get; set; } = "";
    public string RepoName { get; set; } = "";
    public int PrNumber { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public string? Branch { get; set; }
    public string? AuthorLogin { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset PrCreatedAt { get; set; }
    public DateTimeOffset PrUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string HeadSha { get; set; } = "";
    public string? MergeableState { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int ChangedFiles { get; set; }
}
