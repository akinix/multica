using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the TaskToken entity.
/// </summary>
public class TaskTokenConfiguration : IEntityTypeConfiguration<TaskToken>
{
    public void Configure(EntityTypeBuilder<TaskToken> builder)
    {
        builder.ToTable("task_tokens");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(e => e.TaskId)
            .HasColumnName("task_id")
            .IsRequired();

        builder.Property(e => e.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Index on token_hash for fast lookup
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_task_token_hash");

        // Index on expires_at for cleanup queries
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_task_token_expires_at");
    }
}
