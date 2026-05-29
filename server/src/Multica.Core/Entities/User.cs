namespace Multica.Core.Entities;

/// <summary>
/// User entity representing a registered user.
/// Compatible with Go's db.User struct.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Member> Memberships { get; set; } = new List<Member>();
}
