using System.Reflection;
using AIAgents.Core.Services;

namespace AIAgents.Core.Tests.Services;

public sealed class GitHubCodebaseOnboardingServiceContractTests
{
    [Fact]
    public void BuildOrchestrationContractMarkdown_ContainsAdoFirstCompletionProtocol()
    {
        var method = typeof(GitHubCodebaseOnboardingService)
            .GetMethod("BuildOrchestrationContractMarkdown", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var markdown = method!.Invoke(null, null) as string;

        Assert.NotNull(markdown);
        Assert.Contains("Job #1: keep Azure DevOps board/fields/state fully up to date", markdown, StringComparison.Ordinal);
        Assert.Contains("No task is complete until you set ADO fields/state and add completion comment, then re-read and print final values", markdown, StringComparison.Ordinal);
        Assert.Contains("No further info needed.", markdown, StringComparison.Ordinal);
        Assert.Contains("before presenting the brief proposed plan", markdown, StringComparison.Ordinal);
        Assert.Contains("If score meets/exceeds minimum and Autonomy Level > 1", markdown, StringComparison.Ordinal);
    }
}
