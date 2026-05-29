namespace Multica.Core.Entities;

public class VerificationCode
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Attempts { get; set; }
}
