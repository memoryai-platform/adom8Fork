using AIAgents.Core.Configuration;
using AIAgents.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Functions.Functions;

/// <summary>
/// Timer-triggered Azure Function that acts as a safety-net for pending Copilot delegations.
///
/// Runs every 2 minutes. Before timing a delegation out, it checks GitHub for completion
/// signals that may have been missed by the webhook:
/// - delegated issue title no longer contains [WIP]
/// - matched PR title no longer contains [WIP]
/// </summary>
public sealed class CopilotTimeoutChecker
{
    private readonly CopilotOptions _copilotOptions;
    private readonly ICopilotDelegationService _delegationService;
    private readonly IActivityLogger _activityLogger;
    private readonly ICopilotCompletionService _completionService;
    private readonly ILogger<CopilotTimeoutChecker> _logger;

    public CopilotTimeoutChecker(
        IOptions<CopilotOptions> copilotOptions,
        ICopilotDelegationService delegationService,
        IActivityLogger activityLogger,
        ICopilotCompletionService completionService,
        ILogger<CopilotTimeoutChecker> logger)
    {
        _copilotOptions = copilotOptions.Value;
        _delegationService = delegationService;
        _activityLogger = activityLogger;
        _completionService = completionService;
        _logger = logger;
    }

    [Function("CopilotTimeoutChecker")]
    public async Task RunAsync(
        [TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        if (!_copilotOptions.Enabled)
        {
            return;
        }

        var timeout = TimeSpan.FromMinutes(_copilotOptions.TimeoutMinutes);
        var pending = await _delegationService.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var delegation in pending)
        {
            try
            {
                await HandlePendingDelegationAsync(delegation, timeout, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to evaluate pending Copilot delegation WI-{WorkItemId}",
                    delegation.WorkItemId);
            }
        }
    }

    private async Task HandlePendingDelegationAsync(
        CopilotDelegation delegation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var elapsed = DateTime.UtcNow - delegation.DelegatedAt;

        var completionResult = await _completionService.ProbeAndCompletePendingDelegationAsync(
            delegation,
            "Copilot timer poll detected completion signal",
            cancellationToken);

        if (completionResult == CopilotCompletionProbeResult.Completed)
        {
            _logger.LogInformation(
                "Recovered Copilot completion for WI-{WorkItemId} during timer poll",
                delegation.WorkItemId);
            return;
        }

        if (completionResult == CopilotCompletionProbeResult.WaitingForPullRequest)
        {
            _logger.LogInformation(
                "GitHub completion signal detected for WI-{WorkItemId}, but the PR is not ready to reconcile yet. Keeping delegation pending.",
                delegation.WorkItemId);
            return;
        }

        if (elapsed < timeout)
        {
            return;
        }

        _logger.LogWarning(
            "Copilot delegation timed out for WI-{WorkItemId} (waited {Elapsed:F0}m, timeout={Timeout}m)",
            delegation.WorkItemId,
            elapsed.TotalMinutes,
            _copilotOptions.TimeoutMinutes);

        delegation.Status = "TimedOut";
        delegation.CompletedAt = DateTime.UtcNow;
        await _delegationService.UpdateAsync(delegation, cancellationToken);

        await _activityLogger.LogAsync(
            "Coding",
            delegation.WorkItemId,
            $"Copilot timed out after {elapsed.TotalMinutes:F0}m - waiting for explicit resume (no automatic coding re-run).",
            "warning",
            cancellationToken);
    }
}
