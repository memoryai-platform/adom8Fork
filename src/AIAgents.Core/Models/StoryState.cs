using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// The persisted state for a story being processed by the agent pipeline.
/// Serialized to .ado/stories/US-{id}/state.json.
/// </summary>
public sealed class StoryState
{
    [JsonPropertyName("workItemId")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("currentState")]
    public required string CurrentState { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("agents")]
    public Dictionary<string, AgentStatus> Agents { get; set; } = new();

    [JsonPropertyName("artifacts")]
    public ArtifactPaths Artifacts { get; set; } = new();

    [JsonPropertyName("decisions")]
    public List<Decision> Decisions { get; set; } = [];

    [JsonPropertyName("questions")]
    public List<Question> Questions { get; set; } = [];
}
