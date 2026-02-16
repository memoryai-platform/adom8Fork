namespace AIAgents.Functions.Models;

/// <summary>
/// Structured result from agent execution. Replaces exception-based control flow
/// so the dispatcher can make intelligent retry vs. fail-permanently decisions.
/// </summary>
public sealed class AgentResult
{
    /// <summary>Whether the agent completed successfully.</summary>
    public bool Success { get; private init; }

    /// <summary>Error classification for failed results. Null when <see cref="Success"/> is true.</summary>
    public ErrorCategory? Category { get; private init; }

    /// <summary>Human-readable error message. Null when <see cref="Success"/> is true.</summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>Original exception, if any. Null when <see cref="Success"/> is true.</summary>
    public Exception? Exception { get; private init; }

    /// <summary>Total tokens consumed by this agent (for dashboard display).</summary>
    public int TokensUsed { get; init; }

    /// <summary>Estimated cost incurred by this agent (for dashboard display).</summary>
    public decimal CostIncurred { get; init; }

    private AgentResult() { }

    /// <summary>Creates a successful result.</summary>
    public static AgentResult Ok(int tokens = 0, decimal cost = 0m) =>
        new() { Success = true, TokensUsed = tokens, CostIncurred = cost };

    /// <summary>
    /// Creates a failed result with error categorization.
    /// </summary>
    /// <param name="category">Type of failure — drives retry behavior in the dispatcher.</param>
    /// <param name="message">Human-readable description of what went wrong.</param>
    /// <param name="exception">Original exception, if available.</param>
    public static AgentResult Fail(ErrorCategory category, string message, Exception? exception = null) =>
        new()
        {
            Success = false,
            Category = category,
            ErrorMessage = message,
            Exception = exception
        };
}
