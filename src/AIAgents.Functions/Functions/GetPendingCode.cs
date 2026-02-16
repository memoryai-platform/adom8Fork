using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Functions;

/// <summary>
/// HTTP trigger that returns stories awaiting human coding.
/// GET /api/pending-code — returns work items in "Awaiting Code" state.
/// </summary>
public sealed class GetPendingCode
{
    private readonly ILogger<GetPendingCode> _logger;
    private readonly IActivityLogger _activityLogger;

    public GetPendingCode(
        ILogger<GetPendingCode> logger,
        IActivityLogger activityLogger)
    {
        _logger = logger;
        _activityLogger = activityLogger;
    }

    [Function("GetPendingCode")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pending-code")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Pending code request received");

        var recentActivity = await _activityLogger.GetRecentAsync(100, cancellationToken);

        // Find stories where Coding agent posted "awaiting human code" but no Testing started yet
        var pendingStories = recentActivity
            .GroupBy(a => a.WorkItemId)
            .Where(g => g.Key > 0)
            .Where(g =>
            {
                var activities = g.OrderBy(a => a.Timestamp).ToList();
                var hasAwaitingCode = activities.Any(a =>
                    a.Agent == "Coding" &&
                    a.Message.Contains("Awaiting human", StringComparison.OrdinalIgnoreCase));
                var hasTestingStarted = activities.Any(a =>
                    a.Agent == "Testing" &&
                    a.Message.Contains("started", StringComparison.OrdinalIgnoreCase) &&
                    a.Timestamp > activities.Last(x =>
                        x.Agent == "Coding" &&
                        x.Message.Contains("Awaiting human", StringComparison.OrdinalIgnoreCase)).Timestamp);
                return hasAwaitingCode && !hasTestingStarted;
            })
            .Select(g =>
            {
                var codingActivity = g.OrderByDescending(a => a.Timestamp)
                    .First(a => a.Agent == "Coding" &&
                        a.Message.Contains("Awaiting human", StringComparison.OrdinalIgnoreCase));

                return new PendingCodeItem
                {
                    WorkItemId = g.Key,
                    Branch = $"feature/US-{g.Key}",
                    AwaitingSince = codingActivity.Timestamp,
                    PlanAvailable = g.Any(a =>
                        a.Agent == "Planning" &&
                        a.Message.Contains("completed", StringComparison.OrdinalIgnoreCase))
                };
            })
            .OrderBy(p => p.AwaitingSince)
            .ToList();

        return new OkObjectResult(new
        {
            count = pendingStories.Count,
            stories = pendingStories
        });
    }

    private sealed class PendingCodeItem
    {
        public int WorkItemId { get; init; }
        public string Branch { get; init; } = "";
        public DateTime AwaitingSince { get; init; }
        public bool PlanAvailable { get; init; }
    }
}
