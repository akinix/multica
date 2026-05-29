using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class AgentSkillConfiguration : IEntityTypeConfiguration<AgentSkill>
{
    public void Configure(EntityTypeBuilder<AgentSkill> builder)
    {
        builder.HasKey(s => new { s.AgentId, s.SkillId });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Skill>()
            .WithMany()
            .HasForeignKey(s => s.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
