using AIAgents.Core.Constants;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AIAgents.Functions.Tests.Services;

public sealed class WorkItemDecompositionServiceTests
{
    [Fact]
    public async Task SpawnDecompositionAsync_CreatesChildrenInPlanOrder_AndLinksDependencies()
    {
        var adoMock = new Mock<IAzureDevOpsClient>();
        var queueMock = new Mock<IAgentTaskQueue>();
        var created = new Queue<int>(new[] { 101, 102, 103 });

        adoMock.Setup(x => x.CreateChildWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => created.Dequeue());

        var service = new WorkItemDecompositionService(adoMock.Object, queueMock.Object, NullLogger<WorkItemDecompositionService>.Instance);

        var plan = new PlanningResult
        {
            ProblemAnalysis = "p",
            TechnicalApproach = "t",
            AffectedFiles = [],
            Complexity = 5,
            Architecture = "a",
            SubTasks = [],
            Dependencies = [],
            Risks = [],
            Assumptions = [],
            TestingStrategy = "tests",
            FeatureDecomposition =
            [
                new FeatureDecompositionItem { Title = "A", Description = "d1", AcceptanceCriteria = "ac1", PredecessorIndexes = [] },
                new FeatureDecompositionItem { Title = "B", Description = "d2", AcceptanceCriteria = "ac2", PredecessorIndexes = [0] },
                new FeatureDecompositionItem { Title = "C", Description = "d3", AcceptanceCriteria = "ac3", PredecessorIndexes = [1] }
            ]
        };

        await service.SpawnDecompositionAsync(new StoryWorkItem { Id = 77, Title = "Parent", State = "Story Planning", AutonomyLevel = 3 }, plan, "corr-1");

        adoMock.Verify(x => x.CreateChildWorkItemAsync("A", "d1", "ac1", "AI Agent", 3, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.CreateChildWorkItemAsync("B", "d2", "ac2", "New", 3, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.CreateChildWorkItemAsync("C", "d3", "ac3", "New", 3, It.IsAny<CancellationToken>()), Times.Once);

        adoMock.Verify(x => x.AddParentChildLinkAsync(77, 101, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.AddParentChildLinkAsync(77, 102, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.AddParentChildLinkAsync(77, 103, It.IsAny<CancellationToken>()), Times.Once);

        adoMock.Verify(x => x.AddPredecessorSuccessorLinkAsync(101, 102, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.AddPredecessorSuccessorLinkAsync(102, 103, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SpawnDecompositionAsync_TriggersIndependentChildren_Only()
    {
        var adoMock = new Mock<IAzureDevOpsClient>();
        var queueMock = new Mock<IAgentTaskQueue>();
        var created = new Queue<int>(new[] { 201, 202 });

        adoMock.Setup(x => x.CreateChildWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => created.Dequeue());

        var service = new WorkItemDecompositionService(adoMock.Object, queueMock.Object, NullLogger<WorkItemDecompositionService>.Instance);

        var plan = new PlanningResult
        {
            ProblemAnalysis = "p",
            TechnicalApproach = "t",
            AffectedFiles = [],
            Complexity = 3,
            Architecture = "a",
            SubTasks = [],
            Dependencies = [],
            Risks = [],
            Assumptions = [],
            TestingStrategy = "tests",
            FeatureDecomposition =
            [
                new FeatureDecompositionItem { Title = "Independent", Description = "d1", AcceptanceCriteria = "ac1", PredecessorIndexes = [] },
                new FeatureDecompositionItem { Title = "Blocked", Description = "d2", AcceptanceCriteria = "ac2", PredecessorIndexes = [0] }
            ]
        };

        await service.SpawnDecompositionAsync(new StoryWorkItem { Id = 88, Title = "Parent", State = "Story Planning", AutonomyLevel = 4 }, plan, "corr-2");

        queueMock.Verify(x => x.EnqueueAsync(
            It.Is<AgentTask>(t => t.WorkItemId == 201 && t.AgentType == AgentType.Planning && t.CorrelationId == "corr-2"),
            It.IsAny<CancellationToken>()), Times.Once);

        queueMock.Verify(x => x.EnqueueAsync(
            It.Is<AgentTask>(t => t.WorkItemId == 202),
            It.IsAny<CancellationToken>()), Times.Never);

        adoMock.Verify(x => x.UpdateWorkItemFieldAsync(201, CustomFieldNames.Paths.CurrentAIAgent, AIPipelineNames.CurrentAgentValues.Planning, It.IsAny<CancellationToken>()), Times.Once);
        adoMock.Verify(x => x.UpdateWorkItemFieldAsync(202, CustomFieldNames.Paths.CurrentAIAgent, AIPipelineNames.CurrentAgentValues.Planning, It.IsAny<CancellationToken>()), Times.Never);
    }
}
