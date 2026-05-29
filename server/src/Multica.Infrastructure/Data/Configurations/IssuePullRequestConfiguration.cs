using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the IssuePullRequest entity (join table).
/// </summary>
public class IssuePullRequestConfiguration : IEntityTypeConfiguration<IssuePullRequest>
{
    public void Configure(EntityTypeBuilder<IssuePullRequest> builder)
    {
        builder.ToTable("issue_pull_requests");

        builder.HasKey(ipr => new { ipr.IssueId, ipr.PullRequestId });

        builder.Property(ipr => ipr.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(ipr => ipr.PullRequestId)
            .HasColumnName("pull_request_id")
            .IsRequired();

        builder.Property(ipr => ipr.LinkedByType)
            .HasColumnName("linked_by_type")
            .HasMaxLength(20);

        builder.Property(ipr => ipr.LinkedById)
            .HasColumnName("linked_by_id");

        builder.Property(ipr => ipr.LinkedAt)
            .HasColumnName("linked_at")
            .IsRequired();

        builder.Property(ipr => ipr.CloseIntent)
            .HasColumnName("close_intent")
            .IsRequired();

        // Indexes for issue-pr query patterns
        builder.HasIndex(ipr => ipr.IssueId)
            .HasDatabaseName("ix_issue_pull_request_issue_id");

        builder.HasIndex(ipr => ipr.PullRequestId)
            .HasDatabaseName("ix_issue_pull_request_pr_id");

        // Foreign keys
        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(ipr => ipr.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GithubPullRequest>()
            .WithMany()
            .HasForeignKey(ipr => ipr.PullRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
