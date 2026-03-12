using AIAgents.Core.Models;

namespace AIAgents.Functions.Services;

public interface IWorkItemDecompositionService
{
    Task SpawnDecompositionAsync(StoryWorkItem parentWorkItem, PlanningResult planResult, string? correlationId, CancellationToken cancellationToken = default);
}
