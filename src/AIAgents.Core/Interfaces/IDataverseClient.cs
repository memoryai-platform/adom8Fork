using AIAgents.Core.Models;

namespace AIAgents.Core.Interfaces;

/// <summary>
/// Retrieves Dataverse PluginTraceLog entries for monitor processing.
/// </summary>
public interface IDataverseClient
{
    /// <summary>
    /// Returns PluginTraceLog rows with non-null exception details ordered by creation time.
    /// </summary>
    Task<IReadOnlyList<PluginTraceLogEntry>> GetPluginTraceLogsAsync(
        DateTime? createdAfterUtc = null,
        CancellationToken cancellationToken = default);
}
