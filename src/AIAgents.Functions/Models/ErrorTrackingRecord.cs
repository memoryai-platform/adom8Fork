using Azure;

namespace AIAgents.Functions.Models;

/// <summary>
/// Represents a tracked Dataverse plugin error persisted in Azure Table Storage.
/// </summary>
public sealed class ErrorTrackingRecord
{
    public string PluginType { get; set; } = string.Empty;
    public string MessageName { get; set; } = string.Empty;
    public string PrimaryEntity { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string NormalizedMessage { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? Status { get; set; }
    public string? Classification { get; set; }
    public int? WorkItemId { get; set; }
    public DateTime? SuppressedUntilUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int ConsecutiveMissedWindows { get; set; }
    public ETag ETag { get; set; }
}
