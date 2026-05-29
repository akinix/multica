using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Member entity.
/// </summary>
public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Composite unique index on (UserId, WorkspaceId)
        builder.HasIndex(e => new { e.UserId, e.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("ix_member_user_workspace");

        // Index on WorkspaceId for workspace member queries
        builder.HasIndex(e => e.WorkspaceId)
            .HasDatabaseName("ix_member_workspace_id");

        // Foreign key to User
        builder.HasOne(e => e.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to Workspace
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
