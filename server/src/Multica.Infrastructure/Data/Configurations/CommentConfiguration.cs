using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Comment entity.
/// Indexes optimized for comment query patterns.
/// </summary>
public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(c => c.AuthorType)
            .HasColumnName("author_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(c => c.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.ParentId)
            .HasColumnName("parent_id");

        builder.Property(c => c.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(c => c.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(c => c.ResolvedByType)
            .HasColumnName("resolved_by_type")
            .HasMaxLength(20);

        builder.Property(c => c.ResolvedById)
            .HasColumnName("resolved_by_id");

        // Indexes for comment query patterns

        // Composite index on (IssueId, CreatedAt) for chronological listing
        builder.HasIndex(c => new { c.IssueId, c.CreatedAt })
            .HasDatabaseName("ix_comment_issue_created_at");

        // Index on (AuthorType, AuthorId) for author queries
        builder.HasIndex(c => new { c.AuthorType, c.AuthorId })
            .HasDatabaseName("ix_comment_author");

        // Index on (WorkspaceId, IssueId) for workspace-scoped comment queries
        builder.HasIndex(c => new { c.WorkspaceId, c.IssueId })
            .HasDatabaseName("ix_comment_workspace_issue");

        // Foreign key to Issue with cascade delete
        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(c => c.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing foreign key for parent-child relationship
        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
