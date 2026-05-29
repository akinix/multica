namespace Multica.Core.Entities;

public class SkillFile
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
