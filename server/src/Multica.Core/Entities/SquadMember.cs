namespace Multica.Core.Entities;

public class SquadMember
{
    public Guid Id { get; set; }
    public Guid SquadId { get; set; }
    public string MemberType { get; set; } = "";
    public Guid MemberId { get; set; }
    public string Role { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
