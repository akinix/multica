using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

public class TaskUsageHourlyConfiguration : IEntityTypeConfiguration<TaskUsageHourly>
{
    public void Configure(EntityTypeBuilder<TaskUsageHourly> builder)
    {
        builder.HasKey(u => new
        {
            u.BucketHour,
            u.WorkspaceId,
            u.RuntimeId,
            u.AgentId,
            u.Provider,
            u.Model
        });

        builder.Property(u => u.Provider).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Model).HasMaxLength(200).IsRequired();
    }
}
