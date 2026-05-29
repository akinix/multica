using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class TaskUsageHourlyDirtyConfiguration : IEntityTypeConfiguration<TaskUsageHourlyDirty>
{
    public void Configure(EntityTypeBuilder<TaskUsageHourlyDirty> builder)
    {
        builder.HasKey(d => new
        {
            d.BucketHour,
            d.WorkspaceId,
            d.RuntimeId,
            d.AgentId,
            d.Provider,
            d.Model
        });

        builder.Property(d => d.Provider).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Model).HasMaxLength(200).IsRequired();
    }
}
