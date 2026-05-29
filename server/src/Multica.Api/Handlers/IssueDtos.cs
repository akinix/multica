using System.Text.Json.Serialization;

namespace Multica.Api.Handlers;

/// <summary>
/// Response DTO for label data.
/// </summary>
public record LabelResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("workspace_id")]
    public string WorkspaceId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("color")]
    public string Color { get; init; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = "";
}
