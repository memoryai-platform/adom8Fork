namespace AIAgents.Core.Interfaces;

/// <summary>
/// Abstracts repository-level operations that differ between hosting providers
/// (Azure DevOps Repos vs GitHub). Work-item operations stay in <see cref="IAzureDevOpsClient"/>.
///
/// Implementations:
///   - <c>AzureDevOpsRepositoryProvider</c> — uses ADO Git SDK
///   - <c>GitHubRepositoryProvider</c> — uses GitHub REST API
/// </summary>
public interface IRepositoryProvider
{
    /// <summary>
    /// Creates a pull request from <paramref name="sourceBranch"/> to <paramref name="targetBranch"/>.
    /// Returns a provider-specific PR identifier (e.g., PR number).
    /// </summary>
    Task<int> CreatePullRequestAsync(
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges (completes) a pull request. Uses squash-merge by default.
    /// </summary>
    Task MergePullRequestAsync(
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a deployment pipeline/workflow.
    /// For ADO: triggers a pipeline by ID.
    /// For GitHub: dispatches a workflow via workflow_dispatch.
    /// Returns the run ID.
    /// </summary>
    Task<int> TriggerDeploymentAsync(
        string branch,
        CancellationToken cancellationToken = default);
}
