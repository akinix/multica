namespace Multica.Core.Entities;

public class IssueDependency
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid DependsOnIssueId { get; set; }
    public string Type { get; set; } = "";
}
