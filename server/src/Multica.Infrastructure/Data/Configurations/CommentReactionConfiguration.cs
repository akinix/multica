using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the CommentReaction entity.
/// </summary>
public class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> builder)
    {
        builder.ToTable("comment_reaction");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(r => r.CommentId)
            .HasColumnName("comment_id")
            .IsRequired();

        builder.Property(r => r.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(r => r.ActorType)
            .HasColumnName("actor_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(r => r.Emoji)
            .HasColumnName("emoji")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Unique constraint on (CommentId, ActorType, ActorId, Emoji)
        builder.HasIndex(r => new { r.CommentId, r.ActorType, r.ActorId, r.Emoji })
            .IsUnique()
            .HasDatabaseName("ix_comment_reaction_unique");

        // Index on CommentId for bulk loading
        builder.HasIndex(r => r.CommentId)
            .HasDatabaseName("ix_comment_reaction_comment_id");

        // Foreign key to Comment with cascade delete
        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
