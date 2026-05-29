namespace Multica.Core.Entities;

/// <summary>
/// Personal access token entity for PAT authentication.
/// Compatible with Go's db.PersonalAccessToken struct.
/// </summary>
public class PersonalAccessToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
