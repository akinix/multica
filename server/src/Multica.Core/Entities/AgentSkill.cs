namespace Multica.Core.Entities;

public class AgentSkill
{
    public Guid AgentId { get; set; }
    public Guid SkillId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
