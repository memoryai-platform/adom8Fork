using AIAgents.Core.Models;
using AIAgents.Functions.Agents;
using AIAgents.Functions.Tests.Helpers;

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
        string codingGuidelines = "Use C# conventions. Follow SOLID principles.")
    {
        var wi = MockAIResponses.SampleWorkItem(
            id: workItemId,
            description: description,
            acceptanceCriteria: acceptanceCriteria);

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
        Assert.Contains("do NOT create a new branch", body);
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
}
