using AIAgents.Functions.Functions;

namespace AIAgents.Functions.Tests.Functions;

public sealed class McpBridgeWebhookTests
{
    [Theory]
    [InlineData("Planning", "AI Agent", "Planning Agent")]
    [InlineData("Coding", "AI Agent", "Coding Agent")]
    [InlineData("Testing", "AI Agent", "Testing Agent")]
    [InlineData("Review", "AI Agent", "Review Agent")]
    [InlineData("Documentation", "AI Agent", "Documentation Agent")]
    [InlineData("Deployment", "AI Agent", "Deployment Agent")]
    [InlineData("NeedsInfo", "Needs Revision", null)]
    [InlineData("Done", "Code Review", null)]
    public void MapStage_KnownStages_ReturnsExpectedMapping(string stage, string expectedState, string? expectedAgent)
    {
        var mapping = McpBridgeWebhook.MapStage(stage);

        Assert.NotNull(mapping);
        Assert.Equal(expectedState, mapping!.State);
        Assert.Equal(expectedAgent, mapping.CurrentAgent);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("  ")]
    public void MapStage_UnknownStages_ReturnsNull(string stage)
    {
        var mapping = McpBridgeWebhook.MapStage(stage);

        Assert.Null(mapping);
    }
}
