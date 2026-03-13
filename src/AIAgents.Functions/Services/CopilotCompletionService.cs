using System.Net;
using System.Text;
using System.Text.Json;
using AIAgents.Core.Configuration;
using AIAgents.Core.Constants;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Functions.Services;

public interface ICopilotCompletionService
{
    Task<GitHubIssueSnapshot?> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default);
    Task<GitHubPullRequestSnapshot?> FindPullRequestByBaseBranchAsync(string baseBranch, CancellationToken cancellationToken = default);
    Task<bool> TryCompleteFromPullRequestAsync(CopilotDelegation delegation, int prNumber, string copilotBranch, string completionSource, CancellationToken cancellationToken = default);
    Task<bool> TryCompleteFromIssueAsync(CopilotDelegation delegation, string completionSource, CancellationToken cancellationToken = default);
    Task<CopilotCompletionProbeResult> ProbeAndCompletePendingDelegationAsync(CopilotDelegation delegation, string completionSource, CancellationToken cancellationToken = default);
}

public sealed record GitHubIssueSnapshot(int Number, string Title, string State);

public sealed record GitHubPullRequestSnapshot(int Number, string Title, string HeadBranch, string BaseBranch, bool IsDraft, string Body);

public enum CopilotCompletionProbeResult
{
    NoSignal = 0,
    WaitingForPullRequest = 1,
    Completed = 2
}

public sealed class CopilotCompletionService : ICopilotCompletionService
{
    private const string CheckpointLastAgent = "LastAgent";
    private const string CheckpointCurrentAiAgent = "AICurrentAgent";
    private const string CheckpointCompletionComment = "CompletionComment";
    private static readonly string[] PullRequestStates = ["open", "closed"];

    private readonly CopilotOptions _copilotOptions;
    private readonly GitHubOptions _githubOptions;
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IAgentTaskQueue _taskQueue;
    private readonly IActivityLogger _activityLogger;
    private readonly ICopilotDelegationService _delegationService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CopilotCompletionService> _logger;

    public CopilotCompletionService(
        IOptions<CopilotOptions> copilotOptions,
        IOptions<GitHubOptions> githubOptions,
        IAzureDevOpsClient adoClient,
        IAgentTaskQueue taskQueue,
        IActivityLogger activityLogger,
        ICopilotDelegationService delegationService,
        IHttpClientFactory httpClientFactory,
        ILogger<CopilotCompletionService> logger)
    {
        _copilotOptions = copilotOptions.Value;
        _githubOptions = githubOptions.Value;
        _adoClient = adoClient;
        _taskQueue = taskQueue;
        _activityLogger = activityLogger;
        _delegationService = delegationService;
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _logger = logger;
    }

    public async Task<GitHubIssueSnapshot?> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        if (issueNumber <= 0)
        {
            return null;
        }

        var response = await _httpClient.GetAsync(
            $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/issues/{issueNumber}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new GitHubIssueSnapshot(
            root.GetProperty("number").GetInt32(),
            root.GetProperty("title").GetString() ?? string.Empty,
            root.GetProperty("state").GetString() ?? string.Empty);
    }

    public async Task<GitHubPullRequestSnapshot?> FindPullRequestByBaseBranchAsync(string baseBranch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            return null;
        }

        foreach (var state in PullRequestStates)
        {
            var matches = await FindPullRequestsAsync(state, baseBranch, cancellationToken);
            foreach (var pr in matches)
            {
                return pr;
            }
        }

        return null;
    }

    public async Task<CopilotCompletionProbeResult> ProbeAndCompletePendingDelegationAsync(
        CopilotDelegation delegation,
        string completionSource,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(delegation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return CopilotCompletionProbeResult.NoSignal;
        }

        var pr = await FindPullRequestForDelegationAsync(delegation, cancellationToken);
        if (pr is not null && IsTitleReady(pr.Title))
        {
            return await TryCompleteFromPullRequestAsync(
                delegation,
                pr.Number,
                pr.HeadBranch,
                completionSource,
                cancellationToken)
                ? CopilotCompletionProbeResult.Completed
                : CopilotCompletionProbeResult.WaitingForPullRequest;
        }

        if (pr is not null)
        {
            return CopilotCompletionProbeResult.WaitingForPullRequest;
        }

        return CopilotCompletionProbeResult.NoSignal;
    }

    public async Task<bool> TryCompleteFromIssueAsync(
        CopilotDelegation delegation,
        string completionSource,
        CancellationToken cancellationToken = default)
    {
        var pullRequest = await FindPullRequestForDelegationAsync(delegation, cancellationToken);
        if (pullRequest is null)
        {
            _logger.LogInformation(
                "GitHub issue signaled completion for WI-{WorkItemId}, but no matching Copilot PR was found yet. Waiting.",
                delegation.WorkItemId);

            try
            {
                await _activityLogger.LogAsync(
                    "Coding",
                    delegation.WorkItemId,
                    $"GitHub issue is ready, but no matching Copilot PR was found yet for {delegation.BranchName}. Waiting for PR reconciliation.",
                    "info",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Non-critical activity log failed while waiting for PR after issue completion signal for WI-{WorkItemId}",
                    delegation.WorkItemId);
            }

            return false;
        }

        if (!IsTitleReady(pullRequest.Title))
        {
            _logger.LogInformation(
                "GitHub issue signaled completion for WI-{WorkItemId}, but PR #{PrNumber} still has a WIP title. Waiting.",
                delegation.WorkItemId,
                pullRequest.Number);

            try
            {
                await _activityLogger.LogAsync(
                    "Coding",
                    delegation.WorkItemId,
                    $"GitHub issue is ready, but matching PR #{pullRequest.Number} still has [WIP]. Waiting for final PR reconciliation.",
                    "info",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Non-critical activity log failed while waiting for WIP PR after issue completion signal for WI-{WorkItemId}",
                    delegation.WorkItemId);
            }

            return false;
        }

        return await TryCompleteFromPullRequestAsync(
            delegation,
            pullRequest.Number,
            pullRequest.HeadBranch,
            completionSource,
            cancellationToken);
    }

    public async Task<bool> TryCompleteFromPullRequestAsync(
        CopilotDelegation delegation,
        int prNumber,
        string copilotBranch,
        string completionSource,
        CancellationToken cancellationToken = default)
    {
        var workItemId = delegation.WorkItemId;

        try
        {
            await _activityLogger.LogAsync(
                "Coding",
                workItemId,
                $"{completionSource} — reconciling GitHub coding changes",
                "info",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Non-critical activity log failed before reconciliation for WI-{WorkItemId}",
                workItemId);
        }

        var (changedFiles, metrics) = await FetchPrDetailsAsync(prNumber, delegation.DelegatedAt, cancellationToken);
        if (changedFiles.Count == 0)
        {
            _logger.LogInformation(
                "PR #{PrNumber} for WI-{WorkItemId} has no changed files yet — skipping reconciliation",
                prNumber,
                workItemId);

            try
            {
                await _activityLogger.LogAsync(
                    "Coding",
                    workItemId,
                    $"Copilot PR #{prNumber} has no file changes yet — waiting for code",
                    "info",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Non-critical activity log failed for empty-change PR #{PrNumber} WI-{WorkItemId}",
                    prNumber,
                    workItemId);
            }

            return false;
        }

        var reconciledFiles = changedFiles
            .Where(file => file.Status != "removed")
            .Select(file => file.Filename)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isInitializeCodebaseStory = false;
        try
        {
            var workItem = await _adoClient.GetWorkItemAsync(workItemId, cancellationToken);
            isInitializeCodebaseStory = workItem.Tags.Any(tag =>
                string.Equals(tag, AIPipelineNames.InitializeCodebaseTag, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not evaluate InitializeCodebase tag for WI-{WorkItemId}; proceeding with standard Copilot handoff",
                workItemId);
        }

        if (delegation.IssueNumber > 0)
        {
            try
            {
                await CloseIssueAsync(delegation.IssueNumber, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to close issue #{IssueNumber} — may already be closed",
                    delegation.IssueNumber);
            }
        }

        var lastAgentUpdated = false;
        try
        {
            await _adoClient.UpdateWorkItemFieldAsync(workItemId, CustomFieldNames.Paths.LastAgent, "Coding", cancellationToken);
            lastAgentUpdated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update Last Agent for WI-{WorkItemId} during Copilot reconciliation",
                workItemId);
        }

        var nextAgentType = isInitializeCodebaseStory ? AgentType.Deployment : AgentType.Testing;
        var nextAgentValue = nextAgentType == AgentType.Deployment
            ? AIPipelineNames.CurrentAgentValues.Deployment
            : AIPipelineNames.CurrentAgentValues.Testing;

        var currentAgentUpdated = false;
        try
        {
            await _adoClient.UpdateWorkItemFieldAsync(workItemId, CustomFieldNames.Paths.CurrentAIAgent, nextAgentValue, cancellationToken);
            currentAgentUpdated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update Current AI Agent for WI-{WorkItemId} during Copilot reconciliation",
                workItemId);
        }

        var completionCommentAdded = false;
        try
        {
            await _adoClient.AddWorkItemCommentAsync(
                workItemId,
                $"<b>🤖 AI Coding Agent Complete (GitHub Copilot)</b><br/>" +
                $"Strategy: Copilot coding agent<br/>" +
                $"PR: #{prNumber} | Files: {metrics.FilesChanged} | +{metrics.LinesAdded}/-{metrics.LinesDeleted} lines<br/>" +
                $"Duration: {metrics.DurationMinutes:F1} minutes | Commits: {metrics.CommitCount}<br/>" +
                (isInitializeCodebaseStory
                    ? "Initialize Codebase flow → handing off to Deployment agent"
                    : "Coding complete → Azure pipeline resumed at Testing agent"),
                cancellationToken);
            completionCommentAdded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Non-critical ADO comment failed for WI-{WorkItemId} PR #{PrNumber}",
                workItemId,
                prNumber);
        }

        if (_copilotOptions.CheckpointEnforcementEnabled)
        {
            var required = ParseRequiredAdoCheckpoints(_copilotOptions.RequiredAdoCheckpoints);
            var (passed, missing) = EvaluateRequiredCheckpointStatus(required, lastAgentUpdated, currentAgentUpdated, completionCommentAdded);
            if (!passed)
            {
                var missingLabel = string.Join(", ", missing);
                var onlyCurrentAiAgentMissing = missing.Count == 1
                    && string.Equals(missing[0], CheckpointCurrentAiAgent, StringComparison.OrdinalIgnoreCase);

                if (onlyCurrentAiAgentMissing)
                {
                    try
                    {
                        await _adoClient.AddWorkItemCommentAsync(
                            workItemId,
                            $"⚠️ <b>Copilot completion checkpoint warning</b><br/>" +
                            $"PR: #{prNumber}<br/>" +
                            $"Missing update: {CheckpointCurrentAiAgent}<br/>" +
                            "Handoff will continue. Verify Current AI Agent field permissions/configuration.",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to post non-blocking checkpoint warning comment for WI-{WorkItemId}",
                            workItemId);
                    }

                    await _activityLogger.LogAsync(
                        "Coding",
                        workItemId,
                        $"Copilot handoff proceeding for US-{workItemId} with warning: missing optional checkpoint update {CheckpointCurrentAiAgent}.",
                        "warning",
                        cancellationToken);
                }
                else
                {
                    var failMessage = $"Checkpoint enforcement blocked Copilot handoff for US-{workItemId}. Missing required updates: {missingLabel}.";

                    delegation.Status = _copilotOptions.CheckpointFailHard ? "Failed" : "Pending";
                    delegation.CopilotPrNumber = prNumber;
                    delegation.CompletedAt = _copilotOptions.CheckpointFailHard ? DateTime.UtcNow : null;
                    await _delegationService.UpdateAsync(delegation, cancellationToken);

                    try
                    {
                        await _adoClient.AddWorkItemCommentAsync(
                            workItemId,
                            $"⚠️ <b>Copilot completion checkpoint enforcement blocked handoff</b><br/>" +
                            $"PR: #{prNumber}<br/>" +
                            $"Missing required updates: {missingLabel}<br/>" +
                            $"Pipeline did not enqueue {nextAgentType}.",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to post checkpoint-enforcement comment for WI-{WorkItemId}",
                            workItemId);
                    }

                    await _activityLogger.LogAsync(
                        "Coding",
                        workItemId,
                        failMessage,
                        "error",
                        cancellationToken);

                    if (_copilotOptions.CheckpointFailHard)
                    {
                        throw new InvalidOperationException(failMessage);
                    }

                    return false;
                }
            }
        }

        await _taskQueue.EnqueueAsync(new AgentTask
        {
            WorkItemId = workItemId,
            AgentType = nextAgentType,
            CorrelationId = delegation.CorrelationId,
            TriggerSource = nameof(CopilotCompletionService),
            ResumeFromStage = AIPipelineNames.ProcessingState,
            HandoffNote = $"Copilot coding completed via PR #{prNumber}; resumed at {nextAgentType}."
        }, cancellationToken);

        delegation.Status = "Completed";
        delegation.CopilotPrNumber = prNumber;
        delegation.CompletedAt = DateTime.UtcNow;
        await _delegationService.UpdateAsync(delegation, cancellationToken);

        try
        {
            await _activityLogger.LogAsync(
                "Coding",
                workItemId,
                $"Copilot coding agent completed successfully — {metrics.FilesChanged} files, +{metrics.LinesAdded}/-{metrics.LinesDeleted} lines, {metrics.DurationMinutes:F1}m, {metrics.CommitCount} commits. Enqueued {nextAgentType} in Azure pipeline. (1 premium credit)",
                1,
                0m,
                "info",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Non-critical activity log failed for reconciliation success summary on WI-{WorkItemId}",
                workItemId);
        }

        _logger.LogInformation(
            "Copilot completion reconciled PR #{PrNumber} for WI-{WorkItemId} from {Source} — {Files} files, enqueued {NextAgent}",
            prNumber,
            workItemId,
            completionSource,
            reconciledFiles.Count,
            nextAgentType);

        return true;
    }

    internal static bool HasWipMarker(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase);

    internal static bool IsTitleReady(string? title) =>
        !string.IsNullOrWhiteSpace(title) && !HasWipMarker(title);

    internal static bool ShouldHandleIssueAction(string? action) =>
        action is not null &&
        (string.Equals(action, "edited", StringComparison.OrdinalIgnoreCase)
         || string.Equals(action, "reopened", StringComparison.OrdinalIgnoreCase));

    internal static bool PullRequestMatchesDelegation(
        CopilotDelegation delegation,
        GitHubPullRequestSnapshot pullRequest,
        string owner,
        string repo)
    {
        var workItemMarker = $"US-{delegation.WorkItemId}";
        var workItemBranchMarker = $"us-{delegation.WorkItemId}";
        var combinedText = $"{pullRequest.Title}\n{pullRequest.Body}";

        var branchMatch =
            string.Equals(pullRequest.BaseBranch, delegation.BranchName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pullRequest.HeadBranch, delegation.BranchName, StringComparison.OrdinalIgnoreCase);

        var workItemMatch = combinedText.Contains(workItemMarker, StringComparison.OrdinalIgnoreCase);
        var headBranchMatch = pullRequest.HeadBranch.Contains(workItemBranchMarker, StringComparison.OrdinalIgnoreCase);

        var issueReferenceMatch = delegation.IssueNumber > 0 &&
            (combinedText.Contains($"#{delegation.IssueNumber}", StringComparison.OrdinalIgnoreCase) ||
             combinedText.Contains($"{owner}/{repo}#{delegation.IssueNumber}", StringComparison.OrdinalIgnoreCase));

        return branchMatch || workItemMatch || headBranchMatch || issueReferenceMatch;
    }

    internal static IReadOnlyList<string> ParseRequiredAdoCheckpoints(string? configured)
    {
        var defaults = new[]
        {
            CheckpointLastAgent,
            CheckpointCurrentAiAgent,
            CheckpointCompletionComment
        };

        if (string.IsNullOrWhiteSpace(configured))
        {
            return defaults;
        }

        var mapped = configured
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Select(MapCheckpointToken)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return mapped.Count == 0 ? defaults : mapped;
    }

    internal static (bool Passed, List<string> Missing) EvaluateRequiredCheckpointStatus(
        IReadOnlyCollection<string> required,
        bool lastAgentUpdated,
        bool currentAgentUpdated,
        bool completionCommentAdded)
    {
        var missing = new List<string>();

        foreach (var checkpoint in required)
        {
            if (checkpoint.Equals(CheckpointLastAgent, StringComparison.OrdinalIgnoreCase) && !lastAgentUpdated)
            {
                missing.Add(CheckpointLastAgent);
            }
            else if (checkpoint.Equals(CheckpointCurrentAiAgent, StringComparison.OrdinalIgnoreCase) && !currentAgentUpdated)
            {
                missing.Add(CheckpointCurrentAiAgent);
            }
            else if (checkpoint.Equals(CheckpointCompletionComment, StringComparison.OrdinalIgnoreCase) && !completionCommentAdded)
            {
                missing.Add(CheckpointCompletionComment);
            }
        }

        return (missing.Count == 0, missing);
    }

    private static string? MapCheckpointToken(string value) => value.ToLowerInvariant() switch
    {
        "lastagent" or "last_agent" or "last-agent" => CheckpointLastAgent,
        "currentaiagent" or "current_ai_agent" or "current-agent" => CheckpointCurrentAiAgent,
        "completioncomment" or "comment" or "completion_comment" => CheckpointCompletionComment,
        _ => null
    };

    private async Task<GitHubPullRequestSnapshot?> FindPullRequestForDelegationAsync(
        CopilotDelegation delegation,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(delegation.BranchName))
        {
            foreach (var state in PullRequestStates)
            {
                var branchMatches = await FindPullRequestsAsync(state, delegation.BranchName, cancellationToken);
                var branchMatch = branchMatches.FirstOrDefault(pr =>
                    PullRequestMatchesDelegation(delegation, pr, _githubOptions.Owner, _githubOptions.Repo));

                if (branchMatch is not null)
                {
                    return branchMatch;
                }
            }
        }

        foreach (var state in PullRequestStates)
        {
            var candidates = await FindPullRequestsAsync(state, null, cancellationToken);
            var match = candidates.FirstOrDefault(pr =>
                PullRequestMatchesDelegation(delegation, pr, _githubOptions.Owner, _githubOptions.Repo));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<GitHubPullRequestSnapshot>> FindPullRequestsAsync(
        string state,
        string? baseBranch,
        CancellationToken cancellationToken)
    {
        var route = string.IsNullOrWhiteSpace(baseBranch)
            ? $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/pulls?state={state}&per_page=50"
            : $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/pulls?state={state}&base={Uri.EscapeDataString(baseBranch)}&per_page=50";

        var response = await _httpClient.GetAsync(route, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        var pullRequests = new List<GitHubPullRequestSnapshot>();

        foreach (var pr in doc.RootElement.EnumerateArray())
        {
            pullRequests.Add(new GitHubPullRequestSnapshot(
                pr.GetProperty("number").GetInt32(),
                pr.GetProperty("title").GetString() ?? string.Empty,
                pr.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty,
                pr.GetProperty("base").GetProperty("ref").GetString() ?? string.Empty,
                pr.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True,
                pr.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty));
        }

        return pullRequests;
    }

    private async Task<(List<PrFile> Files, CopilotMetrics Metrics)> FetchPrDetailsAsync(
        int prNumber,
        DateTime delegatedAt,
        CancellationToken cancellationToken)
    {
        var prResponse = await _httpClient.GetAsync(
            $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/pulls/{prNumber}",
            cancellationToken);
        prResponse.EnsureSuccessStatusCode();
        var prJson = await prResponse.Content.ReadAsStringAsync(cancellationToken);
        using var prDoc = JsonDocument.Parse(prJson);
        var prRoot = prDoc.RootElement;

        var additions = prRoot.GetProperty("additions").GetInt32();
        var deletions = prRoot.GetProperty("deletions").GetInt32();
        var changedFileCount = prRoot.GetProperty("changed_files").GetInt32();
        var commits = prRoot.GetProperty("commits").GetInt32();

        var filesResponse = await _httpClient.GetAsync(
            $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/pulls/{prNumber}/files?per_page=100",
            cancellationToken);
        filesResponse.EnsureSuccessStatusCode();
        var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
        using var filesDoc = JsonDocument.Parse(filesJson);

        var files = new List<PrFile>();
        foreach (var file in filesDoc.RootElement.EnumerateArray())
        {
            files.Add(new PrFile
            {
                Filename = file.GetProperty("filename").GetString() ?? string.Empty,
                Status = file.GetProperty("status").GetString() ?? string.Empty,
                ContentsUrl = file.TryGetProperty("contents_url", out var contentsUrl)
                    ? contentsUrl.GetString()
                    : null
            });
        }

        return (files, new CopilotMetrics
        {
            PullRequestNumber = prNumber,
            FilesChanged = changedFileCount,
            LinesAdded = additions,
            LinesDeleted = deletions,
            DurationMinutes = (DateTime.UtcNow - delegatedAt).TotalMinutes,
            CommitCount = commits
        });
    }

    private async Task CloseIssueAsync(int issueNumber, CancellationToken cancellationToken)
    {
        var closeBody = JsonSerializer.Serialize(new
        {
            state = "closed",
            labels = new[] { "copilot-completed" }
        });

        using var closeContent = new StringContent(closeBody, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(
            $"repos/{_githubOptions.Owner}/{_githubOptions.Repo}/issues/{issueNumber}",
            closeContent,
            cancellationToken);
    }

    private sealed record PrFile
    {
        public required string Filename { get; init; }
        public required string Status { get; init; }
        public string? ContentsUrl { get; init; }
    }
}
