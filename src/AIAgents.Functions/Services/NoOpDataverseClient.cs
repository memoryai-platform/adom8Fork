using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;

namespace AIAgents.Functions.Services;

/// <summary>
/// Dormant-mode Dataverse client used when Dataverse monitoring is not configured.
/// </summary>
public sealed class NoOpDataverseClient : IDataverseClient
{
    public Task<IReadOnlyList<PluginTraceLogEntry>> GetPluginTraceLogsAsync(
        DateTime? createdAfterUtc = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PluginTraceLogEntry>>(Array.Empty<PluginTraceLogEntry>());
}
