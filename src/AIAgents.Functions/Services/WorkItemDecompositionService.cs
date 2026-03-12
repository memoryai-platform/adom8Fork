using AIAgents.Core.Constants;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Services;

public sealed class WorkItemDecompositionService : IWorkItemDecompositionService
{
    private readonly IAzureDevOpsClient _adoClient;
    private readonly IAgentTaskQueue _taskQueue;
    private readonly ILogger<WorkItemDecompositionService> _logger;

    public WorkItemDecompositionService(
        IAzureDevOpsClient adoClient,
        IAgentTaskQueue taskQueue,
        ILogger<WorkItemDecompositionService> logger)
    {
        _adoClient = adoClient;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    public async Task SpawnDecompositionAsync(StoryWorkItem parentWorkItem, PlanningResult planResult, string? correlationId, CancellationToken cancellationToken = default)
    {
        if (planResult.FeatureDecomposition.Count == 0)
        {
            return;
        }

        var createdChildIds = new List<int>(planResult.FeatureDecomposition.Count);

        for (var index = 0; index < planResult.FeatureDecomposition.Count; index++)
        {
            var child = planResult.FeatureDecomposition[index];
            var blocked = child.PredecessorIndexes.Count > 0;
            var childState = blocked ? "New" : "AI Agent";

            var childId = await _adoClient.CreateChildWorkItemAsync(
                child.Title,
                child.Description,
                child.AcceptanceCriteria,
                childState,
                parentWorkItem.AutonomyLevel,
                cancellationToken);

            createdChildIds.Add(childId);
            await _adoClient.AddParentChildLinkAsync(parentWorkItem.Id, childId, cancellationToken);

            if (!blocked)
            {
                try
                {
                    await _adoClient.UpdateWorkItemFieldAsync(
                        childId,
                        CustomFieldNames.Paths.CurrentAIAgent,
                        AIPipelineNames.CurrentAgentValues.Planning,
                        cancellationToken);
                }
                catch
                {
                }

                await _taskQueue.EnqueueAsync(new AgentTask
                {
                    WorkItemId = childId,
                    AgentType = AgentType.Planning,
                    CorrelationId = correlationId,
                    TriggerSource = "FeatureDecomposition"
                }, cancellationToken);
            }
        }

        for (var index = 0; index < planResult.FeatureDecomposition.Count; index++)
        {
            var child = planResult.FeatureDecomposition[index];
            var successorId = createdChildIds[index];
            foreach (var predecessorIndex in child.PredecessorIndexes)
            {
                if (predecessorIndex < 0 || predecessorIndex >= createdChildIds.Count)
                {
                    continue;
                }

                var predecessorId = createdChildIds[predecessorIndex];
                await _adoClient.AddPredecessorSuccessorLinkAsync(predecessorId, successorId, cancellationToken);
            }
        }

        _logger.LogInformation("Spawned {Count} decomposition children for WI-{WorkItemId}", createdChildIds.Count, parentWorkItem.Id);
    }
}
