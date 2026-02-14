using AIAgents.Functions.Models;

namespace AIAgents.Functions.Services;

/// <summary>
/// Common interface for all agent services.
/// Resolved via keyed DI — each AgentType maps to a specific implementation.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Executes the agent's logic for the given task.
    /// Returns an <see cref="AgentResult"/> indicating success or categorized failure.
    /// </summary>
    /// <param name="task">The agent task to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default);
}
