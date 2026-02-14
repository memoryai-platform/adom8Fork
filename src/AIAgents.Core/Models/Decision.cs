using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// An architecture or implementation decision made by an agent.
/// </summary>
public sealed record Decision
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    [JsonPropertyName("decisionText")]
    public required string DecisionText { get; init; }

    [JsonPropertyName("rationale")]
    public required string Rationale { get; init; }
}
