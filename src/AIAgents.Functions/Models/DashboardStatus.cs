using System.Text.Json.Serialization;

namespace AIAgents.Functions.Models;

/// <summary>
/// Response model for the GET /api/status endpoint consumed by the dashboard.
/// </summary>
public sealed class DashboardStatus
{
    [JsonPropertyName("stories")]
    public required IReadOnlyList<StoryStatus> Stories { get; init; }

    [JsonPropertyName("stats")]
    public required DashboardStats Stats { get; init; }

    [JsonPropertyName("recentActivity")]
    public required IReadOnlyList<ActivityEntry> RecentActivity { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Status of a single story in the agent pipeline.
/// </summary>
public sealed class StoryStatus
{
    [JsonPropertyName("workItemId")]
    public required int WorkItemId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("currentAgent")]
    public string? CurrentAgent { get; init; }

    [JsonPropertyName("progress")]
    public int Progress { get; init; }

    [JsonPropertyName("agents")]
    public required IReadOnlyDictionary<string, string> Agents { get; init; }
}

/// <summary>
/// Aggregate statistics for the dashboard header cards.
/// </summary>
public sealed class DashboardStats
{
    [JsonPropertyName("storiesProcessed")]
    public int StoriesProcessed { get; init; }

    [JsonPropertyName("agentsActive")]
    public int AgentsActive { get; init; }

    [JsonPropertyName("successRate")]
    public double SuccessRate { get; init; }

    [JsonPropertyName("avgProcessingTime")]
    public string AvgProcessingTime { get; init; } = "N/A";
}

/// <summary>
/// A single activity log entry for the dashboard feed.
/// </summary>
public sealed class ActivityEntry
{
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    [JsonPropertyName("workItemId")]
    public required int WorkItemId { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "info";
}
