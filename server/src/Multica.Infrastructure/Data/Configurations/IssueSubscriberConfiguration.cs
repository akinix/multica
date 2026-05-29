using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the IssueSubscriber entity.
/// Maps to the issue_subscriber table with composite primary key.
/// </summary>
public class IssueSubscriberConfiguration : IEntityTypeConfiguration<IssueSubscriber>
{
    public void Configure(EntityTypeBuilder<IssueSubscriber> builder)
    {
        builder.ToTable("issue_subscriber");

        // Composite primary key
        builder.HasKey(e => new { e.IssueId, e.UserType, e.UserId });

        builder.Property(e => e.IssueId)
            .HasColumnName("issue_id")
            .IsRequired();

        builder.Property(e => e.UserType)
            .HasColumnName("user_type")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Foreign key to Issue
        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(e => e.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on (UserType, UserId) for user-scoped queries
        builder.HasIndex(e => new { e.UserType, e.UserId })
            .HasDatabaseName("idx_issue_subscriber_user");
    }
}
