using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIAgents.Core.Services;

/// <summary>
/// <see cref="IRepositoryProvider"/> implementation for Azure DevOps Repos.
/// Uses the ADO Git SDK for PR operations and raw REST for pipeline triggers.
/// </summary>
public sealed class AzureDevOpsRepositoryProvider : IRepositoryProvider, IDisposable
{
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly DeploymentOptions _deployOptions;
    private readonly string _repoName;
    private readonly ILogger<AzureDevOpsRepositoryProvider> _logger;
    private readonly Lazy<VssConnection> _connection;

    public AzureDevOpsRepositoryProvider(
        IOptions<AzureDevOpsOptions> adoOptions,
        IOptions<DeploymentOptions> deployOptions,
        IOptions<GitOptions> gitOptions,
        ILogger<AzureDevOpsRepositoryProvider> logger)
    {
        _adoOptions = adoOptions.Value;
        _deployOptions = deployOptions.Value;
        _logger = logger;

        // Derive repo name from the Git URL (last path segment)
        _repoName = gitOptions.Value.RepositoryUrl
            .TrimEnd('/')
            .Split('/')
            .LastOrDefault()
            ?? throw new InvalidOperationException(
                "Cannot determine repository name from Git:RepositoryUrl configuration.");

        _connection = new Lazy<VssConnection>(() =>
        {
            var credentials = new VssBasicCredential(string.Empty, _adoOptions.Pat);
            return new VssConnection(new Uri(_adoOptions.OrganizationUrl), credentials);
        });
    }

    public async Task<int> CreatePullRequestAsync(
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating ADO PR: {Source} → {Target} in {Repo}",
            sourceBranch, targetBranch, _repoName);

        var gitClient = await _connection.Value.GetClientAsync<GitHttpClient>(cancellationToken);

        var pr = new GitPullRequest
        {
            SourceRefName = $"refs/heads/{sourceBranch}",
            TargetRefName = $"refs/heads/{targetBranch}",
            Title = title,
            Description = description
        };

        var created = await gitClient.CreatePullRequestAsync(
            pr,
            _adoOptions.Project,
            _repoName,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created ADO PR #{PrId}", created.PullRequestId);
        return created.PullRequestId;
    }

    public async Task MergePullRequestAsync(
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing ADO PR #{PrId} in {Repo}", pullRequestId, _repoName);

        var gitClient = await _connection.Value.GetClientAsync<GitHttpClient>(cancellationToken);

        // Fetch to get lastMergeSourceCommit (prevents stale merges)
        var pr = await gitClient.GetPullRequestAsync(
            _adoOptions.Project,
            _repoName,
            pullRequestId,
            cancellationToken: cancellationToken);

        var completionUpdate = new GitPullRequest
        {
            Status = PullRequestStatus.Completed,
            LastMergeSourceCommit = pr.LastMergeSourceCommit,
            CompletionOptions = new GitPullRequestCompletionOptions
            {
                DeleteSourceBranch = true,
                MergeStrategy = GitPullRequestMergeStrategy.Squash,
                MergeCommitMessage = $"Merged PR #{pullRequestId}: {pr.Title}"
            }
        };

        await gitClient.UpdatePullRequestAsync(
            completionUpdate,
            _adoOptions.Project,
            _repoName,
            pullRequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("ADO PR #{PrId} completed (squash merged)", pullRequestId);
    }

    public async Task<int> TriggerDeploymentAsync(
        string branch,
        CancellationToken cancellationToken = default)
    {
        var pipelineId = _deployOptions.PipelineId
            ?? throw new InvalidOperationException(
                "Deployment:PipelineId must be configured for Level 5 autonomy with Azure DevOps.");

        _logger.LogInformation("Triggering ADO pipeline {PipelineId} on branch '{Branch}'",
            pipelineId, branch);

        var baseUrl = _adoOptions.OrganizationUrl.TrimEnd('/');
        var url = $"{baseUrl}/{Uri.EscapeDataString(_adoOptions.Project)}/_apis/pipelines/{pipelineId}/runs?api-version=7.1";

        var requestBody = new
        {
            resources = new
            {
                repositories = new
                {
                    self = new { refName = $"refs/heads/{branch}" }
                }
            }
        };

        using var httpClient = new HttpClient();
        var pat = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_adoOptions.Pat}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", pat);

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var runId = doc.RootElement.GetProperty("id").GetInt32();

        _logger.LogInformation("ADO pipeline run #{RunId} started", runId);
        return runId;
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
