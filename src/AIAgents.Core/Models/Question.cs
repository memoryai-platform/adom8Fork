using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// A question raised by an agent that may need human input.
/// </summary>
public sealed record Question
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    [JsonPropertyName("questionText")]
    public required string QuestionText { get; init; }

    [JsonPropertyName("askedTo")]
    public string AskedTo { get; init; } = "team";

    [JsonPropertyName("answer")]
    public string? Answer { get; init; }

    [JsonPropertyName("answeredAt")]
    public DateTime? AnsweredAt { get; init; }
}
