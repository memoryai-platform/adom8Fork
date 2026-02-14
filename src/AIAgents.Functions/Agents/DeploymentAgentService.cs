using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Functions.Agents;

/// <summary>
/// Deployment agent: makes merge/deploy decisions based on autonomy level and review score.
/// Runs after DocumentationAgent creates the PR.
///
/// Autonomy Levels:
///   1-2: Never reached (dispatcher early-exits).
///   3 (Review &amp; Pause): Assigns a human reviewer, sets state to "Code Review".
///   4 (Auto-Merge): Merges the PR if review score meets threshold, sets "Ready for Deployment".
///   5 (Full Autonomy): Merges + triggers deployment pipeline, sets "Deployed".
/// </summary>
public sealed class DeploymentAgentService : IAgentService
{
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IRepositoryProvider _repoProvider;
    private readonly IGitOperations _gitOps;
    private readonly IStoryContextFactory _contextFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly DeploymentOptions _options;
    private readonly ILogger<DeploymentAgentService> _logger;

    public DeploymentAgentService(
        IAzureDevOpsClient adoClient,
        IRepositoryProvider repoProvider,
        IGitOperations gitOps,
        IStoryContextFactory contextFactory,
        IActivityLogger activityLogger,
        IOptions<DeploymentOptions> options,
        ILogger<DeploymentAgentService> logger)
    {
        _adoClient = adoClient;
        _repoProvider = repoProvider;
        _gitOps = gitOps;
        _contextFactory = contextFactory;
        _activityLogger = activityLogger;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deployment agent starting for WI-{WorkItemId}", task.WorkItemId);

        var workItem = await _adoClient.GetWorkItemAsync(task.WorkItemId, cancellationToken);
        var branchName = $"feature/US-{task.WorkItemId}";
        var repoPath = await _gitOps.EnsureBranchAsync(branchName, cancellationToken);

        await using var context = _contextFactory.Create(task.WorkItemId, repoPath);
        var state = await context.LoadStateAsync(cancellationToken);
        state.CurrentState = "AI Deployment";
        state.Agents["Deployment"] = AgentStatus.InProgress();
        await context.SaveStateAsync(state, cancellationToken);

        var autonomyLevel = workItem.AutonomyLevel;
        var minimumScore = workItem.MinimumReviewScore;

        // Extract review score from previous agent's state
        var reviewScore = ExtractReviewScore(state);

        // Extract PR ID from documentation agent's state
        var prId = ExtractPullRequestId(state);

        _logger.LogInformation(
            "Deployment decision for WI-{WorkItemId}: autonomy={Level}, reviewScore={Score}, minScore={Min}, prId={PrId}",
            task.WorkItemId, autonomyLevel, reviewScore, minimumScore, prId);

        var decision = await MakeDeploymentDecisionAsync(
            workItem, autonomyLevel, reviewScore, minimumScore, prId, cancellationToken);

        // Update state
        state.Agents["Deployment"] = AgentStatus.Completed();
        state.Agents["Deployment"].AdditionalData = new Dictionary<string, object>
        {
            ["autonomyLevel"] = autonomyLevel,
            ["reviewScore"] = reviewScore ?? -1,
            ["decision"] = decision.Action,
            ["reason"] = decision.Reason
        };
        state.CurrentState = decision.FinalState;
        await context.SaveStateAsync(state, cancellationToken);

        // Update ADO work item state
        await _adoClient.UpdateWorkItemStateAsync(workItem.Id, decision.FinalState, cancellationToken);

        // Post summary comment
        await _adoClient.AddWorkItemCommentAsync(workItem.Id,
            $"<b>🤖 AI Deployment Agent</b><br/>" +
            $"<b>Autonomy Level:</b> {autonomyLevel}<br/>" +
            $"<b>Review Score:</b> {reviewScore?.ToString() ?? "N/A"}<br/>" +
            $"<b>Decision:</b> {decision.Action}<br/>" +
            $"<b>Reason:</b> {decision.Reason}<br/>" +
            $"<b>Final State:</b> {decision.FinalState}",
            cancellationToken);

        await _activityLogger.LogAsync(
            "Deployment", task.WorkItemId, decision.Action, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Deployment agent completed for WI-{WorkItemId}: {Action} → {State}",
            task.WorkItemId, decision.Action, decision.FinalState);
    }

    private async Task<DeploymentDecision> MakeDeploymentDecisionAsync(
        StoryWorkItem workItem,
        int autonomyLevel,
        int? reviewScore,
        int minimumScore,
        int? prId,
        CancellationToken cancellationToken)
    {
        // Level 3: Pause for human review
        if (autonomyLevel <= 3)
        {
            return new DeploymentDecision(
                Action: "Assigned for human review",
                Reason: $"Autonomy level {autonomyLevel} requires human approval before merge.",
                FinalState: "Code Review");
        }

        // Levels 4-5: Check review score BEFORE any automated action
        if (reviewScore is null || reviewScore < minimumScore)
        {
            return new DeploymentDecision(
                Action: "Blocked — review score below threshold",
                Reason: $"Review score {reviewScore?.ToString() ?? "N/A"} is below minimum {minimumScore}. " +
                        "Requires human review before merge.",
                FinalState: "Needs Revision");
        }

        // Level 4: Auto-merge only
        if (autonomyLevel == 4)
        {
            if (prId is not null)
            {
                await _repoProvider.MergePullRequestAsync(prId.Value, cancellationToken);

                return new DeploymentDecision(
                    Action: $"Auto-merged PR #{prId}",
                    Reason: $"Review score {reviewScore} meets threshold {minimumScore}. PR merged via squash.",
                    FinalState: "Ready for Deployment");
            }

            return new DeploymentDecision(
                Action: "Skipped merge — no PR ID found",
                Reason: "DocumentationAgent did not record a PR ID. Manual merge required.",
                FinalState: "Ready for Deployment");
        }

        // Level 5: Auto-merge + trigger deployment
        if (autonomyLevel >= 5)
        {
            if (prId is not null)
            {
                await _repoProvider.MergePullRequestAsync(prId.Value, cancellationToken);
            }

            try
            {
                var runId = await _repoProvider.TriggerDeploymentAsync("main", cancellationToken);

                return new DeploymentDecision(
                    Action: $"Auto-merged PR #{prId} and triggered deployment run #{runId}",
                    Reason: $"Review score {reviewScore} meets threshold {minimumScore}. " +
                            $"Full autonomy: merged + deployed via '{_options.PipelineName}'.",
                    FinalState: "Deployed");
            }
            catch (InvalidOperationException ex)
            {
                // No pipeline/workflow configured
                _logger.LogWarning(ex, "Deployment trigger not configured");

                return new DeploymentDecision(
                    Action: $"Auto-merged PR #{prId} — no deployment configured",
                    Reason: $"Review score {reviewScore} meets threshold {minimumScore}. " +
                            "Merged, but no deployment pipeline/workflow configured.",
                    FinalState: "Ready for Deployment");
            }
        }

        // Should not reach here
        return new DeploymentDecision(
            Action: "No action taken",
            Reason: $"Unknown autonomy level {autonomyLevel}",
            FinalState: "Ready for QA");
    }

    private static int? ExtractReviewScore(StoryState state)
    {
        if (state.Agents.TryGetValue("Review", out var reviewStatus)
            && reviewStatus.AdditionalData?.TryGetValue("score", out var scoreObj) == true)
        {
            return scoreObj switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                System.Text.Json.JsonElement je when je.TryGetInt32(out var jInt) => jInt,
                _ => null
            };
        }

        return null;
    }

    private static int? ExtractPullRequestId(StoryState state)
    {
        if (state.Agents.TryGetValue("Documentation", out var docStatus)
            && docStatus.AdditionalData?.TryGetValue("prId", out var prIdObj) == true)
        {
            return prIdObj switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                System.Text.Json.JsonElement je when je.TryGetInt32(out var jInt) => jInt,
                _ => null
            };
        }

        return null;
    }

    private sealed record DeploymentDecision(string Action, string Reason, string FinalState);
}
