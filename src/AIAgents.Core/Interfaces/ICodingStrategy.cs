using AIAgents.Core.Models;

namespace AIAgents.Core.Interfaces;

/// <summary>
/// Strategy interface for coding implementations. Allows the Coding agent
/// to delegate to either the built-in agentic tool-use loop or GitHub Copilot's
/// coding agent based on story complexity and configuration.
/// </summary>
public interface ICodingStrategy
{
    /// <summary>
    /// Executes the coding strategy and returns the result.
    /// </summary>
    /// <param name="context">All context needed for the coding work.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including modified files, usage metrics, and mode identifier.</returns>
    Task<CodingResult> ExecuteAsync(CodingContext context, CancellationToken cancellationToken = default);
}
