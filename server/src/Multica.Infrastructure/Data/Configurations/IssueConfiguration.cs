using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Issue entity.
/// Indexes optimized for Phase 3 query patterns.
/// </summary>
public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Priority)
            .HasColumnName("priority")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.AssigneeType)
            .HasColumnName("assignee_type")
            .HasMaxLength(50);

        builder.Property(e => e.AssigneeId)
            .HasColumnName("assignee_id");

        builder.Property(e => e.CreatorType)
            .HasColumnName("creator_type")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatorId)
            .HasColumnName("creator_id")
            .IsRequired();

        builder.Property(e => e.ParentIssueId)
            .HasColumnName("parent_issue_id");

        builder.Property(e => e.AcceptanceCriteria)
            .HasColumnName("acceptance_criteria")
            .HasColumnType("jsonb");

        builder.Property(e => e.ContextRefs)
            .HasColumnName("context_refs")
            .HasColumnType("jsonb");

        builder.Property(e => e.Position)
            .HasColumnName("position")
            .HasDefaultValue(0);

        builder.Property(e => e.DueDate)
            .HasColumnName("due_date");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(e => e.Number)
            .HasColumnName("number")
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .HasColumnName("project_id");

        builder.Property(e => e.OriginType)
            .HasColumnName("origin_type")
            .HasMaxLength(50);

        builder.Property(e => e.OriginId)
            .HasColumnName("origin_id");

        builder.Property(e => e.FirstExecutedAt)
            .HasColumnName("first_executed_at");

        builder.Property(e => e.StartDate)
            .HasColumnName("start_date");

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Indexes for Phase 3 query patterns

        // Unique index on (WorkspaceId, Number) for issue identifier lookup
        builder.HasIndex(e => new { e.WorkspaceId, e.Number })
            .IsUnique()
            .HasDatabaseName("ix_issue_workspace_number");

        // Index on (WorkspaceId, Status) for status-filtered queries
        builder.HasIndex(e => new { e.WorkspaceId, e.Status })
            .HasDatabaseName("ix_issue_workspace_status");

        // Index on (AssigneeType, AssigneeId) for assignee queries
        builder.HasIndex(e => new { e.AssigneeType, e.AssigneeId })
            .HasDatabaseName("ix_issue_assignee");

        // Composite index on (WorkspaceId, CreatedAt) for pagination queries
        builder.HasIndex(e => new { e.WorkspaceId, e.CreatedAt })
            .HasDatabaseName("ix_issue_workspace_created_at");

        // Index on (WorkspaceId, ProjectId) for project-filtered queries
        builder.HasIndex(e => new { e.WorkspaceId, e.ProjectId })
            .HasDatabaseName("ix_issue_workspace_project_id");

        // Index on (WorkspaceId, ParentIssueId) for child issue queries
        builder.HasIndex(e => new { e.WorkspaceId, e.ParentIssueId })
            .HasDatabaseName("ix_issue_workspace_parent_issue_id");

        // Foreign key to Workspace
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Issues)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing foreign key for parent-child relationship
        builder.HasOne(e => e.ParentIssue)
            .WithMany(e => e.ChildIssues)
            .HasForeignKey(e => e.ParentIssueId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
