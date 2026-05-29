namespace Multica.Core.Entities;

public class IssuePullRequest
{
    public Guid IssueId { get; set; }
    public Guid PullRequestId { get; set; }
    public string? LinkedByType { get; set; }
    public Guid? LinkedById { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public bool CloseIntent { get; set; }
}
