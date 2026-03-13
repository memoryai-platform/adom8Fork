using System.Net;
using System.Net.Http;
using System.Text;
using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AIAgents.Functions.Tests.Services;

public sealed class CopilotCompletionServiceTests
{
    [Fact]
    public void PullRequestMatchesDelegation_LiveCopilotPrShapeTargetingMain_ReturnsTrue()
    {
        var delegation = CreateDelegation();
        var pullRequest = new GitHubPullRequestSnapshot(
            110,
            "feat(dashboard): rebrand to Azure DevOps blue and restore legacy logo",
            "copilot/us-147-update-react-branding",
            "main",
            true,
            """
            Original prompt:
            Implement US-147.
            Fixes toddpick/adom8#109
            Target branch for your PR: `feature/US-147`
            """);

        var matches = CopilotCompletionService.PullRequestMatchesDelegation(
            delegation,
            pullRequest,
            "toddpick",
            "adom8");

        Assert.True(matches);
    }

    [Fact]
    public async Task TryCompleteFromIssueAsync_LiveCopilotPrShapeTargetingMain_CompletesAndEnqueuesTesting()
    {
        var handler = new FakeGitHubHandler();
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=closed&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&per_page=50", BuildPullRequestListJson(title: "feat(dashboard): rebrand to Azure DevOps blue and restore legacy logo"));
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls/110", BuildPullRequestDetailsJson());
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls/110/files?per_page=100", BuildPullRequestFilesJson());
        handler.AddJson(HttpMethod.Patch, "/repos/toddpick/adom8/issues/109", "{}");

        var adoClient = CreateAdoClientMock();
        var taskQueue = new Mock<IAgentTaskQueue>();
        taskQueue.Setup(x => x.EnqueueAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activityLogger = CreateActivityLoggerMock();
        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, adoClient.Object, taskQueue.Object, activityLogger.Object, delegationService.Object);
        var delegation = CreateDelegation();

        var completed = await service.TryCompleteFromIssueAsync(
            delegation,
            "GitHub issue #109 ready",
            CancellationToken.None);

        Assert.True(completed);
        Assert.Equal("Completed", delegation.Status);
        Assert.Equal(110, delegation.CopilotPrNumber);
        taskQueue.Verify(
            x => x.EnqueueAsync(
                It.Is<AgentTask>(task =>
                    task.WorkItemId == 147 &&
                    task.AgentType == AgentType.Testing &&
                    task.CorrelationId == delegation.CorrelationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        delegationService.Verify(
            x => x.UpdateAsync(
                It.Is<CopilotDelegation>(record =>
                    record.WorkItemId == 147 &&
                    record.Status == "Completed" &&
                    record.CopilotPrNumber == 110),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Patch && request.PathAndQuery == "/repos/toddpick/adom8/issues/109");
    }

    [Fact]
    public async Task TryCompleteFromIssueAsync_MatchingPrStillWip_DoesNotResume()
    {
        var handler = new FakeGitHubHandler();
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=closed&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&per_page=50", BuildPullRequestListJson(title: "[WIP] feat(dashboard): rebrand to Azure DevOps blue and restore legacy logo"));

        var adoClient = CreateAdoClientMock();
        var taskQueue = new Mock<IAgentTaskQueue>();
        taskQueue.Setup(x => x.EnqueueAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activityLogger = CreateActivityLoggerMock();
        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, adoClient.Object, taskQueue.Object, activityLogger.Object, delegationService.Object);

        var completed = await service.TryCompleteFromIssueAsync(
            CreateDelegation(),
            "GitHub issue #109 ready",
            CancellationToken.None);

        Assert.False(completed);
        taskQueue.Verify(
            x => x.EnqueueAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()),
            Times.Never);
        delegationService.Verify(
            x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProbeAndCompletePendingDelegationAsync_MainTargetPrReady_Completes()
    {
        var handler = new FakeGitHubHandler();
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=closed&base=feature%2FUS-147&per_page=50", "[]");
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls?state=open&per_page=50", BuildPullRequestListJson(title: "feat(dashboard): rebrand to Azure DevOps blue and restore legacy logo"));
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls/110", BuildPullRequestDetailsJson());
        handler.AddJson(HttpMethod.Get, "/repos/toddpick/adom8/pulls/110/files?per_page=100", BuildPullRequestFilesJson());
        handler.AddJson(HttpMethod.Patch, "/repos/toddpick/adom8/issues/109", "{}");

        var adoClient = CreateAdoClientMock();
        var taskQueue = new Mock<IAgentTaskQueue>();
        taskQueue.Setup(x => x.EnqueueAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activityLogger = CreateActivityLoggerMock();
        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, adoClient.Object, taskQueue.Object, activityLogger.Object, delegationService.Object);

        var result = await service.ProbeAndCompletePendingDelegationAsync(
            CreateDelegation(),
            "Copilot timeout fallback detected completion signal",
            CancellationToken.None);

        Assert.Equal(CopilotCompletionProbeResult.Completed, result);
        taskQueue.Verify(
            x => x.EnqueueAsync(
                It.Is<AgentTask>(task => task.WorkItemId == 147 && task.AgentType == AgentType.Testing),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CopilotCompletionService CreateService(
        FakeGitHubHandler handler,
        IAzureDevOpsClient adoClient,
        IAgentTaskQueue taskQueue,
        IActivityLogger activityLogger,
        ICopilotDelegationService delegationService)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient("GitHub"))
            .Returns(httpClient);

        return new CopilotCompletionService(
            Options.Create(new CopilotOptions
            {
                Enabled = true,
                CheckpointEnforcementEnabled = false
            }),
            Options.Create(new GitHubOptions
            {
                Owner = "toddpick",
                Repo = "adom8",
                Token = "test-token"
            }),
            adoClient,
            taskQueue,
            activityLogger,
            delegationService,
            httpClientFactory.Object,
            NullLogger<CopilotCompletionService>.Instance);
    }

    private static Mock<IAzureDevOpsClient> CreateAdoClientMock()
    {
        var adoClient = new Mock<IAzureDevOpsClient>();
        adoClient.Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoryWorkItem
            {
                Id = 147,
                Title = "US-147",
                State = "AI Agent",
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                ChangedDate = DateTime.UtcNow,
                Tags = []
            });
        adoClient.Setup(x => x.UpdateWorkItemFieldAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        adoClient.Setup(x => x.AddWorkItemCommentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return adoClient;
    }

    private static Mock<IActivityLogger> CreateActivityLoggerMock()
    {
        var activityLogger = new Mock<IActivityLogger>();
        activityLogger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        activityLogger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return activityLogger;
    }

    private static CopilotDelegation CreateDelegation() => new()
    {
        WorkItemId = 147,
        IssueNumber = 109,
        CorrelationId = "corr-147",
        BranchName = "feature/US-147",
        DelegatedAt = DateTime.UtcNow.AddMinutes(-20),
        Status = "Pending"
    };

    private static string BuildPullRequestListJson(string title) =>
        $$"""
        [
          {
            "number": 110,
            "title": "{{title}}",
            "draft": true,
            "body": "Implement US-147\nFixes toddpick/adom8#109\nTarget branch for your PR: `feature/US-147`",
            "head": { "ref": "copilot/us-147-update-react-branding" },
            "base": { "ref": "main" }
          }
        ]
        """;

    private static string BuildPullRequestDetailsJson() =>
        """
        {
          "number": 110,
          "title": "feat(dashboard): rebrand to Azure DevOps blue and restore legacy logo",
          "additions": 42,
          "deletions": 7,
          "changed_files": 2,
          "commits": 1
        }
        """;

    private static string BuildPullRequestFilesJson() =>
        """
        [
          {
            "filename": "dashboard/src/pages/Overview.jsx",
            "status": "modified",
            "contents_url": "https://example.test/contents/overview"
          },
          {
            "filename": "dashboard/src/utils/story.js",
            "status": "modified",
            "contents_url": "https://example.test/contents/story"
          }
        ]
        """;

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        private readonly List<Route> _routes = [];

        public List<CapturedRequest> Requests { get; } = [];

        public void AddJson(HttpMethod method, string pathAndQuery, string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _routes.Add(new Route(method, pathAndQuery, () => CreateJsonResponse(body, statusCode)));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
            Requests.Add(new CapturedRequest(request.Method, pathAndQuery));

            var route = _routes.FirstOrDefault(candidate =>
                candidate.Method == request.Method &&
                string.Equals(candidate.PathAndQuery, pathAndQuery, StringComparison.Ordinal));

            if (route is null)
            {
                throw new InvalidOperationException($"No fake GitHub response registered for {request.Method} {pathAndQuery}");
            }

            return Task.FromResult(route.ResponseFactory());
        }

        private static HttpResponseMessage CreateJsonResponse(string body, HttpStatusCode statusCode) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

        private sealed record Route(HttpMethod Method, string PathAndQuery, Func<HttpResponseMessage> ResponseFactory);
    }

    private sealed record CapturedRequest(HttpMethod Method, string PathAndQuery);
}
