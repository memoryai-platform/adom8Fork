using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// Represents the subset of PluginTraceLog fields used by the Dataverse monitor.
/// </summary>
public sealed class PluginTraceLogEntry
{
    [JsonPropertyName("plugintracelogid")]
    public string? PluginTraceLogId { get; init; }

    [JsonPropertyName("typename")]
    public string? TypeName { get; init; }

    [JsonPropertyName("messagename")]
    public string? MessageName { get; init; }

    [JsonPropertyName("primaryentity")]
    public string? PrimaryEntity { get; init; }

    [JsonPropertyName("mode")]
    public int? Mode { get; init; }

    [JsonPropertyName("depth")]
    public int? Depth { get; init; }

    [JsonPropertyName("createdon")]
    public DateTime CreatedOnUtc { get; init; }

    [JsonPropertyName("exceptiondetails")]
    public string? ExceptionDetails { get; init; }

    [JsonPropertyName("messageblock")]
    public string? MessageBlock { get; init; }
}
