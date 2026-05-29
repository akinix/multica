namespace Multica.Core.Entities;

public class Attachment
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? CommentId { get; set; }
    public string UploaderType { get; set; } = "";
    public Guid UploaderId { get; set; }
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ChatSessionId { get; set; }
    public Guid? ChatMessageId { get; set; }
}
