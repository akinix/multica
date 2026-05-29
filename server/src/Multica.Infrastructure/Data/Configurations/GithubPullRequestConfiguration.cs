using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GithubPullRequest entity.
/// </summary>
public class GithubPullRequestConfiguration : IEntityTypeConfiguration<GithubPullRequest>
{
    public void Configure(EntityTypeBuilder<GithubPullRequest> builder)
    {
        builder.ToTable("github_pull_requests");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(pr => pr.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(pr => pr.InstallationId)
            .HasColumnName("installation_id")
            .IsRequired();

        builder.Property(pr => pr.RepoOwner)
            .HasColumnName("repo_owner")
            .IsRequired();

        builder.Property(pr => pr.RepoName)
            .HasColumnName("repo_name")
            .IsRequired();

        builder.Property(pr => pr.PrNumber)
            .HasColumnName("pr_number")
            .IsRequired();

        builder.Property(pr => pr.Title)
            .HasColumnName("title")
            .IsRequired();

        builder.Property(pr => pr.State)
            .HasColumnName("state")
            .IsRequired();

        builder.Property(pr => pr.HtmlUrl)
            .HasColumnName("html_url")
            .IsRequired();

        builder.Property(pr => pr.Branch)
            .HasColumnName("branch");

        builder.Property(pr => pr.AuthorLogin)
            .HasColumnName("author_login");

        builder.Property(pr => pr.AuthorAvatarUrl)
            .HasColumnName("author_avatar_url");

        builder.Property(pr => pr.MergedAt)
            .HasColumnName("merged_at");

        builder.Property(pr => pr.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(pr => pr.PrCreatedAt)
            .HasColumnName("pr_created_at")
            .IsRequired();

        builder.Property(pr => pr.PrUpdatedAt)
            .HasColumnName("pr_updated_at")
            .IsRequired();

        builder.Property(pr => pr.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(pr => pr.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(pr => pr.HeadSha)
            .HasColumnName("head_sha")
            .IsRequired();

        builder.Property(pr => pr.MergeableState)
            .HasColumnName("mergeable_state");

        builder.Property(pr => pr.Additions)
            .HasColumnName("additions")
            .IsRequired();

        builder.Property(pr => pr.Deletions)
            .HasColumnName("deletions")
            .IsRequired();

        builder.Property(pr => pr.ChangedFiles)
            .HasColumnName("changed_files")
            .IsRequired();

        // Unique constraint on (workspace_id, repo_owner, repo_name, pr_number)
        builder.HasIndex(pr => new { pr.WorkspaceId, pr.RepoOwner, pr.RepoName, pr.PrNumber })
            .IsUnique()
            .HasDatabaseName("ix_github_pull_request_repo_number");

        builder.HasIndex(pr => pr.WorkspaceId)
            .HasDatabaseName("ix_github_pull_request_workspace");
    }
}
