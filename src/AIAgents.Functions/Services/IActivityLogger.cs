using AIAgents.Functions.Models;

namespace AIAgents.Functions.Services;

/// <summary>
/// Logs agent activity to Azure Table Storage for dashboard consumption.
/// </summary>
public interface IActivityLogger
{
    /// <summary>
    /// Logs an activity entry.
    /// </summary>
    Task LogAsync(string agent, int workItemId, string message, string level = "info",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an activity entry with token usage data.
    /// </summary>
    Task LogAsync(string agent, int workItemId, string message, int tokens, decimal cost,
        string level = "info", CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent activity entries for the dashboard.
    /// </summary>
    Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);
}
