namespace Multica.Core.Entities;

public class IssueSubscriber
{
    public Guid IssueId { get; set; }
    public string UserType { get; set; } = "";
    public Guid UserId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
