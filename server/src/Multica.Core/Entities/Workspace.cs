namespace Multica.Core.Entities;

/// <summary>
/// Workspace entity representing a multi-tenant workspace.
/// Compatible with Go's db.Workspace struct.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? IssuePrefix { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}
