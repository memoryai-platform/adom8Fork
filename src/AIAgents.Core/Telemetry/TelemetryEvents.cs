namespace AIAgents.Core.Telemetry;

/// <summary>
/// Standardized Application Insights custom event names used across all agents.
/// Use with <see cref="Microsoft.ApplicationInsights.TelemetryClient.TrackEvent"/> 
/// for consistent telemetry that powers alerting and dashboards.
/// </summary>
public static class TelemetryEvents
{
    // Agent lifecycle
    public const string AgentStarted = "AgentStarted";
    public const string AgentCompleted = "AgentCompleted";
    public const string AgentFailed = "AgentFailed";
    public const string AgentPermanentFailure = "AgentPermanentFailure";

    // Token tracking
    public const string TokensUsed = "TokensUsed";
    public const string CostIncurred = "CostIncurred";

    // State transitions
    public const string StateTransition = "StateTransition";

    // Git operations
    public const string GitCommit = "GitCommit";
    public const string GitPush = "GitPush";
    public const string PullRequestCreated = "PullRequestCreated";
    public const string PullRequestMerged = "PullRequestMerged";

    // Validation
    public const string InputValidationFailed = "InputValidationFailed";
    public const string InputValidationWarning = "InputValidationWarning";

    // Health
    public const string HealthCheckCompleted = "HealthCheckCompleted";

    // Dead letter queue
    public const string DeadLetterProcessed = "DeadLetterProcessed";

    // Circuit breaker
    public const string CircuitBreakerTripped = "CircuitBreakerTripped";
}
