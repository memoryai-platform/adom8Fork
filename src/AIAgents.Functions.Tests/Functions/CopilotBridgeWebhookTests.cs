using AIAgents.Functions.Functions;
using AIAgents.Functions.Services;

namespace AIAgents.Functions.Tests.Functions;

public sealed class CopilotBridgeWebhookTests
{
    [Fact]
    public void ExtractWorkItemId_FromTitle_ReturnsId()
    {
        var result = CopilotBridgeWebhook.ExtractWorkItemId("[US-12345] Implement feature", "");
        Assert.Equal(12345, result);
    }

    [Fact]
    public void ExtractWorkItemId_FromBody_ReturnsId()
    {
        var result = CopilotBridgeWebhook.ExtractWorkItemId("Some PR title", "This implements US-67890 from ADO");
        Assert.Equal(67890, result);
    }

    [Fact]
    public void ExtractWorkItemId_NoMatch_ReturnsNull()
    {
        var result = CopilotBridgeWebhook.ExtractWorkItemId("Fix typo in readme", "No work item reference here");
        Assert.Null(result);
    }

    [Fact]
    public void IsReadyToReconcile_OpenedWithoutWip_IsTrue()
    {
        var result = CopilotBridgeWebhook.IsReadyToReconcile("opened", "Feature implementation", false);
        Assert.True(result);
    }

    [Fact]
    public void IsReadyToReconcile_Edited_WipFalse_True()
    {
        var result = CopilotBridgeWebhook.IsReadyToReconcile("edited", "Feature implementation", false);
        Assert.True(result);
    }

    [Fact]
    public void IsReadyToReconcile_Synchronize_WipTrue_False()
    {
        var result = CopilotBridgeWebhook.IsReadyToReconcile("synchronize", "[WIP] Feature implementation", false);
        Assert.False(result);
    }

    [Fact]
    public void IsReadyToReconcile_DraftPr_IsStillTrueWhenWipRemoved()
    {
        var result = CopilotBridgeWebhook.IsReadyToReconcile("edited", "Feature implementation", true);
        Assert.True(result);
    }

    [Fact]
    public void IsReadyToReconcile_Closed_IsFalse()
    {
        var result = CopilotBridgeWebhook.IsReadyToReconcile("closed", "Feature implementation", false);
        Assert.False(result);
    }

    [Theory]
    [InlineData("[WIP] coding in progress", true)]
    [InlineData("Coding complete", false)]
    [InlineData("", false)]
    public void HasWipMarker_DetectsExpectedValues(string title, bool expected)
    {
        Assert.Equal(expected, CopilotCompletionService.HasWipMarker(title));
    }

    [Theory]
    [InlineData("edited", true)]
    [InlineData("reopened", true)]
    [InlineData("opened", false)]
    [InlineData("closed", false)]
    public void ShouldHandleIssueAction_MatchesSupportedActions(string action, bool expected)
    {
        Assert.Equal(expected, CopilotCompletionService.ShouldHandleIssueAction(action));
    }
}
