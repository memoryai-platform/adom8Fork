using System.Text.Json;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Telemetry;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Functions;

/// <summary>
/// Queue trigger that dispatches agent tasks to the appropriate agent service.
/// Uses .NET 8 keyed DI to resolve the correct IAgentService implementation.
/// Enforces autonomy-level early exits: Level 1 stops after Planning, Level 2 stops after Testing.
/// Handles <see cref="AgentResult"/> from agents: retries transient errors, fails permanently on configuration/data errors.
/// </summary>
public sealed class AgentTaskDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentTaskDispatcher> _logger;
    private readonly IActivityLogger _activityLogger;
    private readonly IAzureDevOpsClient _adoClient;
    private readonly TelemetryClient _telemetry;

    public AgentTaskDispatcher(
        IServiceProvider serviceProvider,
        ILogger<AgentTaskDispatcher> logger,
        IActivityLogger activityLogger,
        IAzureDevOpsClient adoClient,
        TelemetryClient telemetry)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activityLogger = activityLogger;
        _adoClient = adoClient;
        _telemetry = telemetry;
    }

    [Function("AgentTaskDispatcher")]
    public async Task Run(
        [QueueTrigger("agent-tasks", Connection = "AzureWebJobsStorage")] string messageText,
        CancellationToken cancellationToken)
    {
        AgentTask? agentTask;
        try
        {
            agentTask = JsonSerializer.Deserialize<AgentTask>(messageText);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize agent task message: {Message}", messageText);
            throw; // Let the queue infrastructure handle poison messages
        }

        if (agentTask is null)
        {
            _logger.LogError("Deserialized agent task is null");
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        // Autonomy-level early exit: fetch work item to check autonomy level
        // Skip work item lookup for standalone agents (e.g., CodebaseDocumentation with WI-0)
        if (agentTask.WorkItemId > 0)
        {
            var workItem = await _adoClient.GetWorkItemAsync(agentTask.WorkItemId, cancellationToken);
            var autonomyLevel = workItem.AutonomyLevel;

            if (ShouldSkipAgent(autonomyLevel, agentTask.AgentType))
            {
                _logger.LogInformation(
                    "Skipping {AgentType} agent for WI-{WorkItemId}: autonomy level {Level} does not include this stage",
                    agentTask.AgentType, agentTask.WorkItemId, autonomyLevel);

                await _activityLogger.LogAsync(
                    agentTask.AgentType.ToString(),
                    agentTask.WorkItemId,
                    $"Skipped — autonomy level {autonomyLevel} stops before {agentTask.AgentType}",
                    cancellationToken: cancellationToken);

                return;
            }
        }

        _logger.LogInformation(
            "Dispatching {AgentType} agent for WI-{WorkItemId} (correlation: {CorrelationId})",
            agentTask.AgentType, agentTask.WorkItemId, agentTask.CorrelationId);

        var agentKey = agentTask.AgentType.ToString();

        IAgentService? agentService;
        try
        {
            agentService = scope.ServiceProvider.GetRequiredKeyedService<IAgentService>(agentKey);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "No agent service registered for key '{AgentKey}'", agentKey);
            throw;
        }

        await _activityLogger.LogAsync(
            agentKey,
            agentTask.WorkItemId,
            $"{agentKey} agent started processing",
            cancellationToken: cancellationToken);

        _telemetry.TrackEvent(TelemetryEvents.AgentStarted, new Dictionary<string, string>
        {
            [TelemetryProperties.WorkItemId] = agentTask.WorkItemId.ToString(),
            [TelemetryProperties.AgentType] = agentKey,
            [TelemetryProperties.CorrelationId] = agentTask.CorrelationId
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        AgentResult result;

        try
        {
            result = await agentService.ExecuteAsync(agentTask, cancellationToken);
        }
        catch (Exception ex)
        {
            // Agent threw instead of returning AgentResult — treat as Code error
            result = AgentResult.Fail(ErrorCategory.Code, $"Unhandled exception in {agentKey}: {ex.Message}", ex);
        }

        stopwatch.Stop();

        if (result.Success)
        {
            await _activityLogger.LogAsync(
                agentKey,
                agentTask.WorkItemId,
                $"{agentKey} agent completed successfully",
                result.TokensUsed,
                result.CostIncurred,
                cancellationToken: cancellationToken);

            _telemetry.TrackEvent(TelemetryEvents.AgentCompleted, new Dictionary<string, string>
            {
                [TelemetryProperties.WorkItemId] = agentTask.WorkItemId.ToString(),
                [TelemetryProperties.AgentType] = agentKey,
                [TelemetryProperties.CorrelationId] = agentTask.CorrelationId
            },
            new Dictionary<string, double>
            {
                [TelemetryProperties.Duration] = stopwatch.ElapsedMilliseconds
            });

            _logger.LogInformation(
                "{AgentType} agent completed for WI-{WorkItemId} in {Duration}ms",
                agentTask.AgentType, agentTask.WorkItemId, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _telemetry.TrackEvent(TelemetryEvents.AgentFailed, new Dictionary<string, string>
            {
                [TelemetryProperties.WorkItemId] = agentTask.WorkItemId.ToString(),
                [TelemetryProperties.AgentType] = agentKey,
                [TelemetryProperties.CorrelationId] = agentTask.CorrelationId,
                [TelemetryProperties.ErrorCategory] = result.Category.ToString()!,
                [TelemetryProperties.ErrorMessage] = result.ErrorMessage ?? "Unknown error"
            });

            _logger.LogError(
                result.Exception,
                "{AgentType} agent failed for WI-{WorkItemId} ({ErrorCategory}): {ErrorMessage}",
                agentTask.AgentType, agentTask.WorkItemId, result.Category, result.ErrorMessage);

            await _activityLogger.LogAsync(
                agentKey,
                agentTask.WorkItemId,
                $"{agentKey} agent failed ({result.Category}): {result.ErrorMessage}",
                "error",
                cancellationToken);

            // Decide retry vs. permanent failure based on error category
            switch (result.Category)
            {
                case ErrorCategory.Transient:
                    // Let queue retry — throw so message stays in queue
                    throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);

                case ErrorCategory.Configuration:
                case ErrorCategory.Data:
                    // Permanent failure — don't waste retries, notify user
                    if (agentTask.WorkItemId > 0)
                    {
                        await PostFailureCommentAsync(agentTask, result, cancellationToken);
                        await _adoClient.UpdateWorkItemStateAsync(
                            agentTask.WorkItemId, "Agent Failed", cancellationToken);
                    }

                    _telemetry.TrackEvent(TelemetryEvents.AgentPermanentFailure, new Dictionary<string, string>
                    {
                        [TelemetryProperties.WorkItemId] = agentTask.WorkItemId.ToString(),
                        [TelemetryProperties.AgentType] = agentKey,
                        [TelemetryProperties.CorrelationId] = agentTask.CorrelationId,
                        [TelemetryProperties.ErrorCategory] = result.Category.ToString()!
                    });
                    break; // Consume message — no retry

                case ErrorCategory.Code:
                default:
                    // Bug — retry once in case it's transient, then it'll hit DLQ
                    throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Posts a formatted failure comment to the Azure DevOps work item.
    /// </summary>
    private async Task PostFailureCommentAsync(AgentTask task, AgentResult result, CancellationToken ct)
    {
        var recommendation = result.Category switch
        {
            ErrorCategory.Configuration => "Check configuration settings (API keys, PATs, credentials).",
            ErrorCategory.Data => "Review work item content for formatting issues or invalid data.",
            _ => "Check Application Insights logs for details."
        };

        var comment = $"""
            ❌ Agent execution failed — permanent error (will not retry)

            **Failed Agent:** {task.AgentType}
            **Error Category:** {result.Category}
            **Error:** {result.ErrorMessage}

            **Recommended Action:** {recommendation}

            **Troubleshooting:**
            1. Check Application Insights with CorrelationId: {task.CorrelationId}
            2. See TROUBLESHOOTING.md for common solutions
            3. Fix the issue, then set work item state back to re-trigger
            """;

        try
        {
            await _adoClient.AddWorkItemCommentAsync(task.WorkItemId, comment, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post failure comment to WI-{WorkItemId}", task.WorkItemId);
        }
    }

    /// <summary>
    /// Determines if the given agent should be skipped based on the autonomy level.
    /// Level 1 (Plan Only): only Planning runs.
    /// Level 2 (Code Only): Planning, Coding, Testing run.
    /// Levels 3-5: all agents run (Deployment agent handles the rest).
    /// CodebaseDocumentation always runs (it's triggered outside the normal pipeline).
    /// </summary>
    private static bool ShouldSkipAgent(int autonomyLevel, AgentType agentType) => agentType switch
    {
        AgentType.CodebaseDocumentation => false, // standalone, always runs
        _ => autonomyLevel switch
        {
            1 => agentType > AgentType.Planning,
            2 => agentType > AgentType.Testing,
            _ => false
        }
    };
}
