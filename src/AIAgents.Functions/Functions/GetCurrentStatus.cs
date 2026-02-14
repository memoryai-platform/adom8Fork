using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Functions;

/// <summary>
/// HTTP trigger that returns the current pipeline status for the dashboard.
/// GET /api/status
/// </summary>
public sealed class GetCurrentStatus
{
    private readonly ILogger<GetCurrentStatus> _logger;
    private readonly IActivityLogger _activityLogger;

    public GetCurrentStatus(
        ILogger<GetCurrentStatus> logger,
        IActivityLogger activityLogger)
    {
        _logger = logger;
        _activityLogger = activityLogger;
    }

    [Function("GetCurrentStatus")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Dashboard status request received");

        var recentActivity = await _activityLogger.GetRecentAsync(50, cancellationToken);

        // Build story statuses from activity log
        var storyStatuses = BuildStoryStatuses(recentActivity);

        var status = new DashboardStatus
        {
            Stories = storyStatuses,
            Stats = new DashboardStats
            {
                StoriesProcessed = storyStatuses.Count(s =>
                    s.Agents.Values.All(a => a is "completed" or "failed")),
                AgentsActive = storyStatuses.Count(s =>
                    s.Agents.Values.Any(a => a == "in_progress")),
                SuccessRate = CalculateSuccessRate(storyStatuses),
                AvgProcessingTime = "N/A"
            },
            RecentActivity = recentActivity
        };

        return new OkObjectResult(status);
    }

    private static List<StoryStatus> BuildStoryStatuses(IReadOnlyList<ActivityEntry> activities)
    {
        var storyGroups = activities
            .GroupBy(a => a.WorkItemId)
            .Where(g => g.Key > 0);

        var statuses = new List<StoryStatus>();

        foreach (var group in storyGroups)
        {
            var agents = new Dictionary<string, string>();
            string? currentAgent = null;

            foreach (var activity in group.OrderBy(a => a.Timestamp))
            {
                var agent = activity.Agent;
                if (agent == "Orchestrator") continue;

                if (activity.Message.Contains("started", StringComparison.OrdinalIgnoreCase))
                {
                    agents[agent] = "in_progress";
                    currentAgent = agent;
                }
                else if (activity.Message.Contains("completed", StringComparison.OrdinalIgnoreCase))
                {
                    agents[agent] = "completed";
                    if (currentAgent == agent) currentAgent = null;
                }
                else if (activity.Message.Contains("failed", StringComparison.OrdinalIgnoreCase))
                {
                    agents[agent] = "failed";
                    if (currentAgent == agent) currentAgent = null;
                }
            }

            var completedCount = agents.Values.Count(v => v == "completed");
            var totalAgents = 5; // Planning, Coding, Testing, Review, Documentation
            var progress = (int)((double)completedCount / totalAgents * 100);

            statuses.Add(new StoryStatus
            {
                WorkItemId = group.Key,
                Title = $"US-{group.Key}",
                CurrentAgent = currentAgent,
                Progress = progress,
                Agents = agents
            });
        }

        return statuses;
    }

    private static double CalculateSuccessRate(List<StoryStatus> stories)
    {
        if (stories.Count == 0) return 100.0;

        var completed = stories.Count(s =>
            s.Agents.Values.Any() && s.Agents.Values.All(a => a == "completed"));
        var failed = stories.Count(s =>
            s.Agents.Values.Any(a => a == "failed"));

        var total = completed + failed;
        return total > 0 ? Math.Round((double)completed / total * 100, 1) : 100.0;
    }
}
