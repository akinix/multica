namespace Multica.Core.Auth;

/// <summary>
/// Identity returned for validated cloud PAT tokens (mcn_).
/// Uses short JSON keys (o, i, r) matching Go implementation.
/// </summary>
public record CloudPatIdentity
{
    public string OwnerId { get; init; } = "";
    public string InstanceId { get; init; } = "";
    public string InstanceRecordId { get; init; } = "";
}
