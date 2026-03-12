using System.Text.Json;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Functions;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIAgents.Functions.Tests.Functions;

public sealed class PRMergeOrchestrationFunctionTests
{
    [Fact]
    public async Task SingleMerge_UnlocksSingleSuccessor()
    {
        var ado = new Mock<IAzureDevOpsClient>();
        var dedupe = new Mock<IMergeEventDeduplicationStore>();
        dedupe.Setup(x => x.TryProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ado.Setup(x => x.GetSuccessorIdsAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync([456]);
        ado.Setup(x => x.GetPredecessorIdsAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync([123]);
        ado.Setup(x => x.GetWorkItemAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(new StoryWorkItem
        {
            Id = 123,
            Title = "US-123",
            State = "Done",
            CreatedDate = DateTime.UtcNow,
            ChangedDate = DateTime.UtcNow
        });

        var sut = new PRMergeOrchestrationFunction(ado.Object, dedupe.Object, Mock.Of<ILogger<PRMergeOrchestrationFunction>>());
        var root = JsonDocument.Parse(MergedPayload(1, "feature/US-123", "abc")).RootElement;

        var processed = await sut.ProcessMergedPullRequestAsync(root, CancellationToken.None);

        Assert.True(processed);
        ado.Verify(x => x.UpdateWorkItemStateAsync(456, "AI Agent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MultiPredecessor_UnlocksOnlyAfterFinalPredecessorDone()
    {
        var ado = new Mock<IAzureDevOpsClient>();
        var dedupe = new Mock<IMergeEventDeduplicationStore>();
        dedupe.Setup(x => x.TryProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ado.Setup(x => x.GetSuccessorIdsAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync([200]);
        ado.Setup(x => x.GetPredecessorIdsAsync(200, It.IsAny<CancellationToken>())).ReturnsAsync([100, 101]);
        ado.Setup(x => x.GetWorkItemAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(BuildStory(100, "Done"));
        ado.Setup(x => x.GetWorkItemAsync(101, It.IsAny<CancellationToken>())).ReturnsAsync(BuildStory(101, "Active"));

        var sut = new PRMergeOrchestrationFunction(ado.Object, dedupe.Object, Mock.Of<ILogger<PRMergeOrchestrationFunction>>());
        var root = JsonDocument.Parse(MergedPayload(9, "feature/US-100", "sha1")).RootElement;

        await sut.ProcessMergedPullRequestAsync(root, CancellationToken.None);
        ado.Verify(x => x.UpdateWorkItemStateAsync(200, "AI Agent", It.IsAny<CancellationToken>()), Times.Never);

        ado.Setup(x => x.GetWorkItemAsync(101, It.IsAny<CancellationToken>())).ReturnsAsync(BuildStory(101, "Done"));
        await sut.ProcessMergedPullRequestAsync(root, CancellationToken.None);
        ado.Verify(x => x.UpdateWorkItemStateAsync(200, "AI Agent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateMergeEvent_IsSuppressed()
    {
        var ado = new Mock<IAzureDevOpsClient>();
        var dedupe = new Mock<IMergeEventDeduplicationStore>();
        dedupe.SetupSequence(x => x.TryProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        ado.Setup(x => x.GetSuccessorIdsAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync([456]);
        ado.Setup(x => x.GetPredecessorIdsAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync([123]);
        ado.Setup(x => x.GetWorkItemAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(BuildStory(123, "Done"));

        var sut = new PRMergeOrchestrationFunction(ado.Object, dedupe.Object, Mock.Of<ILogger<PRMergeOrchestrationFunction>>());
        var root = JsonDocument.Parse(MergedPayload(77, "feature/US-123", "sha77")).RootElement;

        await sut.ProcessMergedPullRequestAsync(root, CancellationToken.None);
        await sut.ProcessMergedPullRequestAsync(root, CancellationToken.None);

        ado.Verify(x => x.UpdateWorkItemStateAsync(456, "AI Agent", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static StoryWorkItem BuildStory(int id, string state) => new()
    {
        Id = id,
        Title = $"US-{id}",
        State = state,
        CreatedDate = DateTime.UtcNow,
        ChangedDate = DateTime.UtcNow
    };

    private static string MergedPayload(int prNumber, string headRef, string mergeSha) => $$"""
{
  "action": "closed",
  "pull_request": {
    "number": {{prNumber}},
    "merged": true,
    "merge_commit_sha": "{{mergeSha}}",
    "head": { "ref": "{{headRef}}" }
  }
}
""";
}
