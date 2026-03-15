using AIAgents.Functions.Models;
using Azure;

namespace AIAgents.Functions.Services;

/// <summary>
/// Dormant-mode error tracking service used when Dataverse monitoring is not configured.
/// </summary>
public sealed class NoOpErrorTrackingService : IErrorTrackingService
{
    public Task<ErrorTrackingRecord?> GetAsync(
        string pluginType,
        string fingerprint,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ErrorTrackingRecord?>(null);

    public Task<ErrorTrackingRecord> UpsertAsync(
        ErrorTrackingRecord record,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(record);

    public Task<DateTime?> GetWatermarkAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<DateTime?>(null);

    public Task SetWatermarkAsync(
        DateTime watermarkUtc,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
