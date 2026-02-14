using System.Text.Json.Serialization;

namespace AIAgents.Functions.Models;

/// <summary>
/// Health check response model returned by the /api/health endpoint.
/// Reports status of all system components with response times and details.
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>Overall system status: healthy, degraded, or unhealthy.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>When the health check was performed (UTC).</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Individual component check results keyed by component name.</summary>
    [JsonPropertyName("checks")]
    public Dictionary<string, ComponentCheck> Checks { get; init; } = new();

    /// <summary>Application version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>Deployment environment (dev, staging, prod).</summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }
}

/// <summary>
/// Status of an individual health check component (ADO, queue, AI API, etc.).
/// </summary>
public sealed class ComponentCheck
{
    /// <summary>Component status: healthy, degraded, or unhealthy.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Response time in milliseconds, if applicable.</summary>
    [JsonPropertyName("responseTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ResponseTime { get; init; }

    /// <summary>Queue message count, if applicable.</summary>
    [JsonPropertyName("messageCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MessageCount { get; init; }

    /// <summary>Poison queue message count, if applicable.</summary>
    [JsonPropertyName("poisonMessageCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PoisonMessageCount { get; init; }

    /// <summary>Additional detail or error message.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>Missing environment variables, if any.</summary>
    [JsonPropertyName("missingVars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MissingVars { get; init; }
}
