using AIAgents.Functions.Models;
using Azure;

namespace AIAgents.Functions.Services;

/// <summary>
/// Persists tracked Dataverse plugin errors and the scan watermark.
/// </summary>
public interface IErrorTrackingService
{
    /// <summary>
    /// Retrieves a tracked error by plugin type and fingerprint.
    /// </summary>
    Task<ErrorTrackingRecord?> GetAsync(
        string pluginType,
        string fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a tracked error using optimistic concurrency when an ETag is supplied.
    /// </summary>
    Task<ErrorTrackingRecord> UpsertAsync(
        ErrorTrackingRecord record,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last successful PluginTraceLog watermark, or null when none has been stored.
    /// </summary>
    Task<DateTime?> GetWatermarkAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the last successful PluginTraceLog watermark using optimistic concurrency when an ETag is supplied.
    /// </summary>
    Task SetWatermarkAsync(
        DateTime watermarkUtc,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default);
}
