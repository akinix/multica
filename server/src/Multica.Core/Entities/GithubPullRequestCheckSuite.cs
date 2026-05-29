namespace Multica.Core.Entities;

public class GithubPullRequestCheckSuite
{
    public Guid PrId { get; set; }
    public long SuiteId { get; set; }
    public string HeadSha { get; set; } = "";
    public long AppId { get; set; }
    public string? Conclusion { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
