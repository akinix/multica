using System.Text.Json;

namespace Multica.Core.Entities;

/// <summary>
/// Issue entity representing a task/issue in the system.
/// Compatible with Go's db.Issue struct.
/// </summary>
public class Issue
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? AssigneeType { get; set; }
    public Guid? AssigneeId { get; set; }
    public string CreatorType { get; set; } = "";
    public Guid CreatorId { get; set; }
    public Guid? ParentIssueId { get; set; }
    public JsonDocument AcceptanceCriteria { get; set; } = JsonDocument.Parse("[]");
    public JsonDocument ContextRefs { get; set; } = JsonDocument.Parse("[]");
    public double Position { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Number { get; set; }
    public Guid? ProjectId { get; set; }
    public string? OriginType { get; set; }
    public Guid? OriginId { get; set; }
    public DateTimeOffset? FirstExecutedAt { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public JsonDocument? Metadata { get; set; }

    // Navigation properties
    public Workspace? Workspace { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> ChildIssues { get; set; } = new List<Issue>();
}
