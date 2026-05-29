using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class AgentTaskQueueConfiguration : IEntityTypeConfiguration<AgentTaskQueue>
{
    public void Configure(EntityTypeBuilder<AgentTaskQueue> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => new { t.AgentId, t.Status });
        builder.HasIndex(t => new { t.Status, t.Priority });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(t => t.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(t => t.IssueId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AgentRuntime>()
            .WithMany()
            .HasForeignKey(t => t.RuntimeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AgentTaskQueue>()
            .WithMany()
            .HasForeignKey(t => t.ParentTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.Status).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Error).HasMaxLength(4000);
        builder.Property(t => t.FailureReason).HasMaxLength(500);
        builder.Property(t => t.WaitReason).HasMaxLength(500);
        builder.Property(t => t.SessionId).HasMaxLength(255);
        builder.Property(t => t.WorkDir).HasMaxLength(1000);
        builder.Property(t => t.TriggerSummary).HasMaxLength(2000);
        builder.Property(t => t.Result).HasColumnType("jsonb");
        builder.Property(t => t.Context).HasColumnType("jsonb");
    }
}
