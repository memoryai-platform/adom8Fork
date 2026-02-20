using AIAgents.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Functions;

/// <summary>
/// Clears the dashboard live activity feed only.
/// POST /api/clear-activity
/// </summary>
public sealed class ClearActivityFeed
{
    private readonly IActivityLogger _activityLogger;
    private readonly ILogger<ClearActivityFeed> _logger;

    public ClearActivityFeed(
        IActivityLogger activityLogger,
        ILogger<ClearActivityFeed> logger)
    {
        _activityLogger = activityLogger;
        _logger = logger;
    }

    [Function("ClearActivityFeed")]
    public async Task<IActionResult> Execute(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "clear-activity")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        try
        {
            await _activityLogger.ClearFeedAsync(cancellationToken);
            _logger.LogInformation("Dashboard live activity feed cleared.");

            return new OkObjectResult(new
            {
                status = "activity_cleared"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear live activity feed");
            return new ObjectResult(new
            {
                status = "error",
                message = ex.Message
            })
            { StatusCode = 500 };
        }
    }
}
