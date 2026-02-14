using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// Tracks file paths of generated artifacts for a story.
/// </summary>
public sealed class ArtifactPaths
{
    [JsonPropertyName("code")]
    public List<string> Code { get; set; } = [];

    [JsonPropertyName("tests")]
    public List<string> Tests { get; set; } = [];

    [JsonPropertyName("docs")]
    public List<string> Docs { get; set; } = [];
}
