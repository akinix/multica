using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.WorkspaceId, a.Name });

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AgentRuntime>()
            .WithMany()
            .HasForeignKey(a => a.RuntimeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Name).HasMaxLength(255).IsRequired();
        builder.Property(a => a.RuntimeMode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Visibility).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Status).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Model).HasMaxLength(100);
        builder.Property(a => a.ThinkingLevel).HasMaxLength(50);
        builder.Property(a => a.RuntimeConfig).HasColumnType("jsonb");
        builder.Property(a => a.CustomEnv).HasColumnType("jsonb");
        builder.Property(a => a.CustomArgs).HasColumnType("jsonb");
        builder.Property(a => a.McpConfig).HasColumnType("jsonb");
    }
}
