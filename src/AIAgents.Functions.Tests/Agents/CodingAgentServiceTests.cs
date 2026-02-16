using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Agents;
using AIAgents.Functions.Models;
using AIAgents.Functions.Tests.Helpers;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AIAgents.Functions.Tests.Agents;

/// <summary>
/// Tests for CodingAgentService covering complexity assessment,
/// auto-code mode, handoff mode, parsing, and error handling.
/// </summary>
public sealed class CodingAgentServiceTests
{
    private readonly Mock<IAIClientFactory> _aiFactoryMock = new();
    private readonly Mock<IAIClient> _aiClientMock = new();
    private readonly Mock<IAzureDevOpsClient> _adoMock = new();
    private readonly Mock<IGitOperations> _gitMock = new();
    private readonly Mock<IStoryContextFactory> _contextFactoryMock = new();
    private readonly Mock<IStoryContext> _contextMock = new();
    private readonly Mock<ICodebaseContextProvider> _codebaseMock = new();
    private readonly Mock<IAgentTaskQueue> _taskQueueMock = new();
    private readonly Mock<IActivityLogger> _activityLoggerMock = new();

    private StoryState _capturedState = null!;

    public CodingAgentServiceTests()
    {
        _aiFactoryMock.Setup(f => f.GetClientForAgent("Coding", It.IsAny<StoryModelOverrides?>())).Returns(_aiClientMock.Object);
        _contextFactoryMock.Setup(f => f.Create(It.IsAny<int>(), It.IsAny<string>())).Returns(_contextMock.Object);
    }

    private CodingAgentService CreateService()
    {
        return new CodingAgentService(
            _aiFactoryMock.Object, _adoMock.Object, _gitMock.Object,
            _contextFactoryMock.Object, _codebaseMock.Object,
            NullLogger<CodingAgentService>.Instance, _taskQueueMock.Object,
            _activityLoggerMock.Object);
    }

    private void SetupHappyPath(string? aiResponse = null)
    {
        var wi = MockAIResponses.SampleWorkItem();
        var state = MockAIResponses.SampleState(wi.Id, "AI Code");

        _adoMock.Setup(a => a.GetWorkItemAsync(wi.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wi);
        _gitMock.Setup(g => g.EnsureBranchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(@"C:\repos\test");
        _gitMock.Setup(g => g.ListFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "src/Program.cs" });
        _gitMock.Setup(g => g.ReadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Console.WriteLine(\"Hello\");");

        _contextMock.Setup(c => c.LoadStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);
        _contextMock.Setup(c => c.SaveStateAsync(It.IsAny<StoryState>(), It.IsAny<CancellationToken>()))
            .Callback<StoryState, CancellationToken>((s, _) => _capturedState = s).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.ReadArtifactAsync("PLAN.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Plan\nModify `src/Program.cs`.\nSome plan content.");

        _aiClientMock.Setup(a => a.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<AICompletionOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AICompletionResult
            {
                Content = aiResponse ?? MockAIResponses.ValidCodingResponse,
                Usage = new TokenUsageData
                {
                    InputTokens = 1500, OutputTokens = 3000, TotalTokens = 4500,
                    EstimatedCost = 0.03m, Model = "claude-sonnet-4-20250514"
                }
            });

        _codebaseMock.Setup(c => c.LoadRelevantContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");
    }

    // ========== AUTO MODE TESTS ==========

    [Fact]
    public async Task ExecuteAsync_AutoMode_CreatesNewFiles()
    {
        SetupHappyPath(); // ValidCodingResponse: complexity=2, 2 new files
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        // Should write 2 new files
        _gitMock.Verify(g => g.WriteFileAsync(
            It.IsAny<string>(), "src/Services/RegistrationService.cs", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _gitMock.Verify(g => g.WriteFileAsync(
            It.IsAny<string>(), "src/Models/User.cs", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_TracksArtifacts()
    {
        SetupHappyPath();
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal(2, _capturedState.Artifacts.Code.Count);
        Assert.Contains("src/Services/RegistrationService.cs", _capturedState.Artifacts.Code);
        Assert.Contains("src/Models/User.cs", _capturedState.Artifacts.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_TransitionsToAITest()
    {
        SetupHappyPath();
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal("AI Test", _capturedState.CurrentState);
        Assert.Equal("completed", _capturedState.Agents["Coding"].Status);
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_EnqueuesTestingAgent()
    {
        SetupHappyPath();
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        _taskQueueMock.Verify(q => q.EnqueueAsync(
            It.Is<AgentTask>(t => t.AgentType == AgentType.Testing), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_TracksAutoModeInState()
    {
        SetupHappyPath();
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal("auto", _capturedState.Agents["Coding"].AdditionalData!["mode"]);
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_AppliesSearchReplaceEdits()
    {
        SetupHappyPath(aiResponse: MockAIResponses.ValidCodingResponseWithEdits);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        // Should write the edited file (replacing search text)
        _gitMock.Verify(g => g.WriteFileAsync(
            It.IsAny<string>(), "src/Program.cs",
            It.Is<string>(content => content.Contains("Hello World")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_TracksTokens()
    {
        SetupHappyPath();
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.True(_capturedState.TokenUsage.Agents.ContainsKey("Coding"));
        Assert.Equal(4500, _capturedState.TokenUsage.Agents["Coding"].TotalTokens);
    }

    // ========== HANDOFF MODE TESTS ==========

    [Fact]
    public async Task ExecuteAsync_HandoffMode_SetsAwaitingCodeState()
    {
        SetupHappyPath(aiResponse: MockAIResponses.ComplexCodingResponse);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal("Awaiting Code", _capturedState.CurrentState);
        Assert.Equal("completed", _capturedState.Agents["Coding"].Status);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffMode_DoesNotEnqueueTesting()
    {
        SetupHappyPath(aiResponse: MockAIResponses.ComplexCodingResponse);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        _taskQueueMock.Verify(q => q.EnqueueAsync(
            It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffMode_TracksHandoffModeInState()
    {
        SetupHappyPath(aiResponse: MockAIResponses.ComplexCodingResponse);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal("handoff", _capturedState.Agents["Coding"].AdditionalData!["mode"]);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffMode_PostsAdoComment()
    {
        SetupHappyPath(aiResponse: MockAIResponses.ComplexCodingResponse);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        _adoMock.Verify(a => a.AddWorkItemCommentAsync(12345,
            It.Is<string>(c => c.Contains("Awaiting Human Code")), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========== MALFORMED RESPONSE TESTS ==========

    [Fact]
    public async Task ExecuteAsync_MalformedResponse_HandsOffToHuman()
    {
        SetupHappyPath(aiResponse: MockAIResponses.MalformedCodingResponse);
        var service = CreateService();

        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        // Malformed JSON → complexity defaults to 5 → handoff
        Assert.Equal("Awaiting Code", _capturedState.CurrentState);
        _taskQueueMock.Verify(q => q.EnqueueAsync(
            It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ========== EDGE CASES ==========

    [Fact]
    public async Task ExecuteAsync_NoPlan_StillRuns()
    {
        SetupHappyPath();
        _contextMock.Setup(c => c.ReadArtifactAsync("PLAN.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService();
        await service.ExecuteAsync(new AgentTask { WorkItemId = 12345, AgentType = AgentType.Coding });

        Assert.Equal("AI Test", _capturedState.CurrentState);
    }

    // ========== PARSING UNIT TESTS ==========

    [Fact]
    public void ParseCodingDecision_ValidJson_ParsesCorrectly()
    {
        var decision = CodingAgentService.ParseCodingDecision(MockAIResponses.ValidCodingResponse);

        Assert.Equal(2, decision.Complexity);
        Assert.Equal(90, decision.Confidence);
        Assert.Equal(2, decision.NewFiles.Count);
        Assert.Empty(decision.Edits);
    }

    [Fact]
    public void ParseCodingDecision_MalformedJson_DefaultsToHandoff()
    {
        var decision = CodingAgentService.ParseCodingDecision(MockAIResponses.MalformedCodingResponse);

        Assert.Equal(5, decision.Complexity);
        Assert.Equal(0, decision.Confidence);
    }

    [Fact]
    public void ParseCodingDecision_WithEdits_ParsesEdits()
    {
        var decision = CodingAgentService.ParseCodingDecision(MockAIResponses.ValidCodingResponseWithEdits);

        Assert.Equal(1, decision.Complexity);
        Assert.Single(decision.Edits);
        Assert.Equal("src/Program.cs", decision.Edits[0].File);
    }

    [Fact]
    public void ExtractReferencedFiles_FindsFilesInPlan()
    {
        var plan = "We need to modify `src/Services/MyService.cs` and `src/Program.cs`.";
        var files = new List<string> { "src/Program.cs", "src/Services/MyService.cs", "src/Models/Unrelated.cs" };

        var result = CodingAgentService.ExtractReferencedFiles(plan, files);

        Assert.Contains("src/Program.cs", result);
        Assert.Contains("src/Services/MyService.cs", result);
        Assert.DoesNotContain("src/Models/Unrelated.cs", result);
    }

    [Fact]
    public void ExtractReferencedFiles_HandlesEmptyPlan()
    {
        var result = CodingAgentService.ExtractReferencedFiles("", new List<string> { "src/Program.cs" });
        Assert.Empty(result);
    }
}
