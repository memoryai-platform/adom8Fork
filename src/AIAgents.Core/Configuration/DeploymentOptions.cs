namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration options for the deployment pipeline integration.
/// Bound to the "Deployment" configuration section.
/// </summary>
public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>
    /// The display name of the deployment pipeline (for logging/comments only).
    /// </summary>
    public string PipelineName { get; init; } = "Deploy-To-Production";

    /// <summary>
    /// The numeric Azure DevOps pipeline ID to trigger.
    /// Required for Level 5 autonomy (full deployment).
    /// </summary>
    public int? PipelineId { get; init; }

    /// <summary>
    /// Whether to run a health check after deployment.
    /// Placeholder for future auto-rollback feature.
    /// </summary>
    public bool RequireHealthCheck { get; init; }

    /// <summary>
    /// The URL to poll for post-deployment health checks.
    /// Only used when <see cref="RequireHealthCheck"/> is true.
    /// </summary>
    public string? HealthCheckUrl { get; init; }

    /// <summary>
    /// Default autonomy level to use when the work item field is not set.
    /// 1 = Plan Only, 2 = Code Only, 3 = Review & Pause, 4 = Auto-Merge, 5 = Full Autonomy.
    /// </summary>
    public int DefaultAutonomyLevel { get; init; } = 3;

    /// <summary>
    /// Default minimum review score when the work item field is not set.
    /// </summary>
    public int DefaultMinimumReviewScore { get; init; } = 85;
}
