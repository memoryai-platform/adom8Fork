using AIAgents.Functions.Functions;
using AIAgents.Functions.Models;

namespace AIAgents.Functions.Tests.Functions;

/// <summary>
/// Tests for DeadLetterQueueHandler's static comment formatting method.
/// The timer-triggered Run() method requires real Azure Storage and cannot
/// be unit tested without infrastructure. FormatDeadLetterComment is internal static
/// and specifically designed for testability.
/// </summary>
public sealed class DeadLetterQueueHandlerTests
{
    [Fact]
    public void FormatDeadLetterComment_IncludesAgentType()
    {
        var task = new AgentTask
        {
            WorkItemId = 42,
            AgentType = AgentType.Coding,
            CorrelationId = "abc123"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("Coding", comment);
    }

    [Fact]
    public void FormatDeadLetterComment_IncludesCorrelationId()
    {
        var task = new AgentTask
        {
            WorkItemId = 42,
            AgentType = AgentType.Testing,
            CorrelationId = "corr-xyz"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("corr-xyz", comment);
    }

    [Fact]
    public void FormatDeadLetterComment_IncludesFailureEmoji()
    {
        var task = new AgentTask
        {
            WorkItemId = 1,
            AgentType = AgentType.Planning,
            CorrelationId = "test"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("❌", comment);
    }

    [Fact]
    public void FormatDeadLetterComment_IncludesTroubleshootingSteps()
    {
        var task = new AgentTask
        {
            WorkItemId = 1,
            AgentType = AgentType.Review,
            CorrelationId = "test"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("Application Insights", comment);
        Assert.Contains("TROUBLESHOOTING.md", comment);
    }

    [Fact]
    public void FormatDeadLetterComment_IncludesRetryInstructions()
    {
        var task = new AgentTask
        {
            WorkItemId = 1,
            AgentType = AgentType.Documentation,
            CorrelationId = "test"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("retry", comment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatDeadLetterComment_IncludesEnqueueTimestamp()
    {
        var task = new AgentTask
        {
            WorkItemId = 1,
            AgentType = AgentType.Planning,
            CorrelationId = "test"
        };

        var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

        Assert.Contains("Originally Enqueued", comment);
    }

    [Fact]
    public void FormatDeadLetterComment_AllAgentTypes_ProduceValidComment()
    {
        foreach (var agentType in Enum.GetValues<AgentType>())
        {
            var task = new AgentTask
            {
                WorkItemId = 99,
                AgentType = agentType,
                CorrelationId = "multi-test"
            };

            var comment = DeadLetterQueueHandler.FormatDeadLetterComment(task);

            Assert.False(string.IsNullOrWhiteSpace(comment), $"Comment for {agentType} should not be empty");
            Assert.Contains(agentType.ToString(), comment);
        }
    }
}
