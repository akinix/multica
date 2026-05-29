using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the DaemonToken entity.
/// </summary>
public class DaemonTokenConfiguration : IEntityTypeConfiguration<DaemonToken>
{
    public void Configure(EntityTypeBuilder<DaemonToken> builder)
    {
        builder.ToTable("daemon_tokens");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(e => e.DaemonId)
            .HasColumnName("daemon_id")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Index on token_hash for fast lookup
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_daemon_token_hash");

        // Index on workspace_id for workspace queries
        builder.HasIndex(e => e.WorkspaceId)
            .HasDatabaseName("ix_daemon_token_workspace_id");
    }
}
