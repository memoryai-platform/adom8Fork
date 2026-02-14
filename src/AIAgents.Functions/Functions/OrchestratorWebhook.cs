using System.Text.Json;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Functions;

/// <summary>
/// HTTP trigger that receives Azure DevOps Service Hook webhooks.
/// Routes work item state changes into the agent pipeline by enqueuing AgentTask messages.
/// </summary>
public sealed class OrchestratorWebhook
{
    private readonly ILogger<OrchestratorWebhook> _logger;
    private readonly IActivityLogger _activityLogger;
    private readonly QueueClient _queueClient;

    // Maps ADO work item states to the agent that processes them
    private static readonly Dictionary<string, AgentType> s_stateToAgent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Story Planning"] = AgentType.Planning,
        ["AI Code"] = AgentType.Coding,
        ["AI Test"] = AgentType.Testing,
        ["AI Review"] = AgentType.Review,
        ["AI Docs"] = AgentType.Documentation
    };

    public OrchestratorWebhook(
        ILogger<OrchestratorWebhook> logger,
        IActivityLogger activityLogger,
        IConfiguration configuration)
    {
        _logger = logger;
        _activityLogger = activityLogger;

        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage is required.");
        _queueClient = new QueueClient(connectionString, "agent-tasks");
        _queueClient.CreateIfNotExists();
    }

    [Function("OrchestratorWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhook")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received webhook request");

        string body;
        using (var reader = new StreamReader(req.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new BadRequestObjectResult(new { error = "Empty request body" });
        }

        ServiceHookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ServiceHookPayload>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse webhook payload");
            return new BadRequestObjectResult(new { error = "Invalid JSON payload" });
        }

        if (payload?.Resource is null)
        {
            return new BadRequestObjectResult(new { error = "Missing resource in payload" });
        }

        // Get the work item ID from wherever it exists in the payload
        var workItemId = payload.Resource.WorkItemId > 0
            ? payload.Resource.WorkItemId
            : payload.Resource.Revision?.Id ?? payload.Resource.Id;

        if (workItemId <= 0)
        {
            return new BadRequestObjectResult(new { error = "Could not determine work item ID" });
        }

        // Determine the new state
        var newState = payload.Resource.Fields?.State?.NewValue;

        if (string.IsNullOrEmpty(newState))
        {
            // Try to get state from revision fields
            if (payload.Resource.Revision?.Fields?.TryGetValue("System.State", out var stateElement) == true)
            {
                newState = stateElement.GetString();
            }
        }

        if (string.IsNullOrEmpty(newState) || !s_stateToAgent.TryGetValue(newState, out var agentType))
        {
            _logger.LogInformation(
                "State '{NewState}' for WI-{WorkItemId} does not map to any agent, skipping",
                newState, workItemId);
            return new OkObjectResult(new { status = "skipped", reason = $"State '{newState}' is not an agent trigger" });
        }

        // Enqueue the agent task
        var agentTask = new AgentTask
        {
            WorkItemId = workItemId,
            AgentType = agentType
        };

        var messageJson = JsonSerializer.Serialize(agentTask);
        var base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson));
        await _queueClient.SendMessageAsync(base64Message, cancellationToken);

        await _activityLogger.LogAsync(
            "Orchestrator",
            workItemId,
            $"Enqueued {agentType} agent for state '{newState}'",
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Enqueued {AgentType} task for WI-{WorkItemId} (state: {NewState})",
            agentType, workItemId, newState);

        return new OkObjectResult(new
        {
            status = "queued",
            workItemId,
            agent = agentType.ToString(),
            correlationId = agentTask.CorrelationId
        });
    }
}
