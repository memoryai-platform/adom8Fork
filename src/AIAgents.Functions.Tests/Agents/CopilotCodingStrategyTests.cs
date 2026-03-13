using System.Net;
using System.Text;
using System.Text.Json;
using AIAgents.Core.Models;
using AIAgents.Functions.Agents;
using AIAgents.Functions.Services;
using AIAgents.Functions.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIAgents.Functions.Tests.Agents;

/// <summary>
/// Tests for CopilotCodingStrategy — validates issue body generation
/// and prompt formatting for the Copilot coding agent.
/// </summary>
public sealed class CopilotCodingStrategyTests
{
    private static CodingContext CreateContext(
        int workItemId = 12345,
        string plan = "# Plan\n1. Modify `src/Service.cs`\n2. Add new endpoint",
        string branchName = "feature/US-12345",
        string? description = "As a user, I want to register with my email.",
        string? acceptanceCriteria = "- Users can register\n- Emails validated",
        string codingGuidelines = "Use C# conventions. Follow SOLID principles.",
        int autonomyLevel = 3)
    {
        var wi = MockAIResponses.SampleWorkItem(
            id: workItemId,
            description: description,
            acceptanceCriteria: acceptanceCriteria,
            autonomyLevel: autonomyLevel);

        return new CodingContext
        {
            WorkItemId = workItemId,
            RepositoryPath = @"C:\repos\test",
            State = MockAIResponses.SampleState(workItemId, "AI Code"),
            WorkItem = wi,
            PlanMarkdown = plan,
            CodingGuidelines = codingGuidelines,
            ExistingFilesSummary = "src/Program.cs\nsrc/Service.cs",
            BranchName = branchName,
            CorrelationId = "test-corr-123"
        };
    }

    // ========== BUILD ISSUE BODY TESTS ==========

    [Fact]
    public void BuildIssueBody_ContainsBranchInstructions()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("feature/US-12345", body);
        Assert.Contains("Create your working branch from this base", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsWorkItemId()
    {
        var context = CreateContext(workItemId: 67890);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("US-67890", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsPlan()
    {
        var context = CreateContext(plan: "# Special Plan\nDo something unique");
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("Special Plan", body);
        Assert.Contains("Do something unique", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsAcceptanceCriteria()
    {
        var context = CreateContext(acceptanceCriteria: "- Must handle edge cases\n- Must be performant");
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("Must handle edge cases", body);
        Assert.Contains("Must be performant", body);
        Assert.Contains("Acceptance Criteria", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsCodingGuidelines()
    {
        var context = CreateContext(codingGuidelines: "Follow DDD patterns");
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("Follow DDD patterns", body);
        Assert.Contains("Coding Guidelines", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsDoNotModifyTestsInstruction()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("Do NOT modify test files", body);
    }

    [Fact]
    public void BuildIssueBody_OmitsAcceptanceCriteria_WhenNull()
    {
        var context = CreateContext(acceptanceCriteria: null);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("Acceptance Criteria", body);
    }

    [Fact]
    public void BuildIssueBody_OmitsDescription_WhenNull()
    {
        var context = CreateContext(description: null);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("## Description", body);
    }

    [Fact]
    public void BuildIssueBody_IncludesDescription_WhenPresent()
    {
        var context = CreateContext(description: "Custom description for Copilot");
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("## Description", body);
        Assert.Contains("Custom description for Copilot", body);
    }

    [Fact]
    public void BuildIssueBody_OmitsCodingGuidelines_WhenEmpty()
    {
        var context = CreateContext(codingGuidelines: "");
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("## Coding Guidelines", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsStoryTitle()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains(context.WorkItem.Title, body);
    }

    [Fact]
    public void BuildIssueBody_ContainsImplementationPlanHeader()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("## Implementation Plan", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsImportantNotes()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("## Important Notes", body);
        Assert.Contains("Follow the implementation plan", body);
        Assert.Contains("Match existing code style", body);
        Assert.Contains("Ensure correct syntax", body);
        Assert.Contains("Do NOT orchestrate ADO stage transitions", body);
        Assert.Contains("[WIP]", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsAdoFirstCompletionProtocol()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("coding-only", body);
        Assert.Contains("Azure orchestrates Planning/Testing/Review/Documentation/Deployment", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsOrchestrationContractInstructions()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain(".agent/ORCHESTRATION_CONTRACT.md", body);
        Assert.Contains("This assignment is coding-only", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsDynamicMinimumReviewScore()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("AI Minimum Review Score", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsAutonomyLevel()
    {
        var context = CreateContext(autonomyLevel: 4);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("AI Autonomy Level:** 4", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsAutonomyNormalizationGuidance()
    {
        var context = CreateContext(autonomyLevel: 3);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("Autonomy Normalization", body);
    }

    [Fact]
    public void BuildIssueBody_AutonomyLevel1_IncludesPlanningOnlyGuardrail()
    {
        var context = CreateContext(autonomyLevel: 1);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.Contains("## Autonomy Level 1 Guardrail", body);
        Assert.Contains("Do not implement code changes", body);
        Assert.Contains("full deep analysis", body);
        Assert.Contains("consolidated Needs Revision comment", body);
        Assert.Contains("No further info needed.", body);
        Assert.Contains("before presenting your brief proposed plan", body);
        Assert.Contains("Needs Revision", body);
    }

    [Fact]
    public void BuildIssueBody_ContainsAutonomyGateForContinueToCoding()
    {
        var context = CreateContext(autonomyLevel: 3);
        var body = CopilotCodingStrategy.BuildIssueBody(context);

        Assert.DoesNotContain("continue to Coding", body);
    }

    // ========== AGENT NAME TESTS ==========

    [Fact]
    public void BuildIssueBody_IncludesAgentName_WhenProvided()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context, "claude");

        Assert.Contains("**Assigned Agent:** @claude", body);
    }

    [Fact]
    public void BuildIssueBody_OmitsAgentName_WhenNull()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context, null);

        Assert.DoesNotContain("Assigned Agent", body);
    }

    [Fact]
    public void BuildIssueBody_OmitsAgentName_WhenEmpty()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context, "");

        Assert.DoesNotContain("Assigned Agent", body);
    }

    [Fact]
    public void BuildIssueBody_ShowsCopilotAgent()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context, "copilot");

        Assert.Contains("**Assigned Agent:** @copilot", body);
    }

    [Fact]
    public void BuildIssueBody_ShowsCodexAgent()
    {
        var context = CreateContext();
        var body = CopilotCodingStrategy.BuildIssueBody(context, "codex");

        Assert.Contains("**Assigned Agent:** @codex", body);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentDuplicateTriggers_CreateOnlyOneGitHubIssue()
    {
        var githubOptions = Options.Create(new AIAgents.Core.Configuration.GitHubOptions
        {
            Owner = "toddpick",
            Repo = "adom8",
            Token = "test-token",
            BaseBranch = "main"
        });
        var copilotOptions = Options.Create(new AIAgents.Core.Configuration.CopilotOptions
        {
            Enabled = true,
            CreateIssue = true,
            Model = "copilot"
        });

        var delegationService = new DelayedInMemoryDelegationService(TimeSpan.FromMilliseconds(150));
        var logger = new Mock<ILogger>();
        var handler = new FakeGitHubHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var strategy = new CopilotCodingStrategy(
            githubOptions,
            copilotOptions,
            delegationService,
            logger.Object,
            httpClient: httpClient);

        var context = CreateContext(workItemId: 148, branchName: "feature/US-148");

        var first = strategy.ExecuteAsync(context, CancellationToken.None);
        var second = strategy.ExecuteAsync(CreateContext(workItemId: 148, branchName: "feature/US-148"), CancellationToken.None);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, handler.IssueCreateCount);
        Assert.All(results, result => Assert.Equal("copilot-delegated", result.Mode));
        Assert.Contains(results, result => result.CopilotMetrics?.IssueNumber == 101);
        Assert.Contains(results, result => result.Summary.Contains("Already delegated to Copilot", StringComparison.Ordinal));

        var delegation = await delegationService.GetByWorkItemIdAsync(148, CancellationToken.None);
        Assert.NotNull(delegation);
        Assert.Equal(101, delegation!.IssueNumber);
        Assert.Equal("Pending", delegation.Status);
    }

    private sealed class DelayedInMemoryDelegationService : ICopilotDelegationService
    {
        private readonly TimeSpan _lookupDelay;
        private readonly object _gate = new();
        private CopilotDelegation? _delegation;

        public DelayedInMemoryDelegationService(TimeSpan lookupDelay)
        {
            _lookupDelay = lookupDelay;
        }

        public Task CreateAsync(CopilotDelegation delegation, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _delegation = Clone(delegation);
            }

            return Task.CompletedTask;
        }

        public async Task<CopilotDelegation?> GetByWorkItemIdAsync(int workItemId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_lookupDelay, cancellationToken);

            lock (_gate)
            {
                return _delegation is null || _delegation.WorkItemId != workItemId
                    ? null
                    : Clone(_delegation);
            }
        }

        public Task<CopilotDelegation?> GetByIssueNumberAsync(int issueNumber, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(
                    _delegation is not null && _delegation.IssueNumber == issueNumber
                        ? Clone(_delegation)
                        : null);
            }
        }

        public Task UpdateAsync(CopilotDelegation delegation, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _delegation = Clone(delegation);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CopilotDelegation>> GetTimedOutAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CopilotDelegation>>(Array.Empty<CopilotDelegation>());
        }

        public Task<IReadOnlyList<CopilotDelegation>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                IReadOnlyList<CopilotDelegation> result = _delegation is null
                    ? Array.Empty<CopilotDelegation>()
                    : new[] { Clone(_delegation) };
                return Task.FromResult(result);
            }
        }

        private static CopilotDelegation Clone(CopilotDelegation delegation) => new()
        {
            WorkItemId = delegation.WorkItemId,
            IssueNumber = delegation.IssueNumber,
            CorrelationId = delegation.CorrelationId,
            BranchName = delegation.BranchName,
            DelegatedAt = delegation.DelegatedAt,
            Status = delegation.Status,
            CopilotPrNumber = delegation.CopilotPrNumber,
            CompletedAt = delegation.CompletedAt
        };
    }

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        private int _issueCounter = 100;

        public int IssueCreateCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/git/ref/heads/feature%2FUS-148", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, new
                {
                    @ref = "refs/heads/feature/US-148",
                    @object = new { sha = "abc123" }
                });
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/issues", StringComparison.Ordinal))
            {
                IssueCreateCount++;
                await Task.Delay(200, cancellationToken);
                var issueNumber = Interlocked.Increment(ref _issueCounter);
                return Json(HttpStatusCode.Created, new { number = issueNumber });
            }

            if (request.Method == HttpMethod.Post && path.Contains("/assignees", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.Created, new { });
            }

            if (request.Method == HttpMethod.Get && path.Contains("/issues/101", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, new
                {
                    assignees = new[]
                    {
                        new { login = "copilot" }
                    }
                });
            }

            if (request.Method == HttpMethod.Post && path.Contains("/comments", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.Created, new { });
            }

            if (request.Method == HttpMethod.Patch && path.Contains("/issues/101", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, new { });
            }

            throw new InvalidOperationException($"Unexpected GitHub request: {request.Method} {path}");
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
        }
    }
}
