using System.Text.Json;

namespace Multica.Core.Entities;

public class ProjectResource
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string ResourceType { get; set; } = "";
    public JsonDocument ResourceRef { get; set; } = JsonDocument.Parse("{}");
    public string? Label { get; set; }
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
