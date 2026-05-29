using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Attachment entity.
/// </summary>
public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(a => a.WorkspaceId)
            .HasColumnName("workspace_id")
            .IsRequired();

        builder.Property(a => a.IssueId)
            .HasColumnName("issue_id");

        builder.Property(a => a.CommentId)
            .HasColumnName("comment_id");

        builder.Property(a => a.UploaderType)
            .HasColumnName("uploader_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.UploaderId)
            .HasColumnName("uploader_id")
            .IsRequired();

        builder.Property(a => a.Filename)
            .HasColumnName("filename")
            .IsRequired();

        builder.Property(a => a.Url)
            .HasColumnName("url")
            .IsRequired();

        builder.Property(a => a.ContentType)
            .HasColumnName("content_type")
            .IsRequired();

        builder.Property(a => a.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.ChatSessionId)
            .HasColumnName("chat_session_id");

        builder.Property(a => a.ChatMessageId)
            .HasColumnName("chat_message_id");

        // Indexes for attachment query patterns
        builder.HasIndex(a => new { a.IssueId, a.WorkspaceId })
            .HasDatabaseName("ix_attachment_issue_workspace");

        builder.HasIndex(a => new { a.CommentId, a.WorkspaceId })
            .HasDatabaseName("ix_attachment_comment_workspace");

        builder.HasIndex(a => a.WorkspaceId)
            .HasDatabaseName("ix_attachment_workspace");
    }
}
