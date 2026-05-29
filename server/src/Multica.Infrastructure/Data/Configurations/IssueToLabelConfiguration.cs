using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class IssueToLabelConfiguration : IEntityTypeConfiguration<IssueToLabel>
{
    public void Configure(EntityTypeBuilder<IssueToLabel> builder)
    {
        builder.HasKey(l => new { l.IssueId, l.LabelId });

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(l => l.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<IssueLabel>()
            .WithMany()
            .HasForeignKey(l => l.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
