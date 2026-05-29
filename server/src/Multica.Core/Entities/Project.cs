namespace Multica.Core.Entities;

public class Project
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Status { get; set; } = "";
    public string? LeadType { get; set; }
    public Guid? LeadId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Priority { get; set; } = "";
}
