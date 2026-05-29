using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class GithubPullRequestCheckSuiteConfiguration : IEntityTypeConfiguration<GithubPullRequestCheckSuite>
{
    public void Configure(EntityTypeBuilder<GithubPullRequestCheckSuite> builder)
    {
        builder.HasKey(c => new { c.PrId, c.SuiteId });

        builder.HasOne<GithubPullRequest>()
            .WithMany()
            .HasForeignKey(c => c.PrId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(c => c.HeadSha).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Conclusion).HasMaxLength(50);
        builder.Property(c => c.Status).HasMaxLength(50).IsRequired();
    }
}
