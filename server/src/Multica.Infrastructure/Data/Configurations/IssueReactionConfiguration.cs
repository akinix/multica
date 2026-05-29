using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the IssueReaction entity.
/// </summary>
public class IssueReactionConfiguration : IEntityTypeConfiguration<IssueReaction>
{
    public void Configure(EntityTypeBuilder<IssueReaction> builder)
    {
        builder.ToTable("issue_reaction");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(r => r.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(r => r.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(r => r.ActorType)
            .HasColumnName("actor_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(r => r.Emoji)
            .HasColumnName("emoji")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Unique constraint on (IssueId, ActorType, ActorId, Emoji)
        builder.HasIndex(r => new { r.IssueId, r.ActorType, r.ActorId, r.Emoji })
            .IsUnique()
            .HasDatabaseName("ix_issue_reaction_unique");

        // Index on IssueId for bulk loading
        builder.HasIndex(r => r.IssueId)
            .HasDatabaseName("ix_issue_reaction_issue_id");

        // Foreign key to Issue with cascade delete
        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(r => r.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
