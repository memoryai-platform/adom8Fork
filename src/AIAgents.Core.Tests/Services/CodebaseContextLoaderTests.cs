using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIAgents.Core.Tests.Services;

public sealed class CodebaseContextLoaderTests
{
    [Fact]
    public async Task LoadRelevantContextAsync_IncludesOrchestrationContractBeforeContextIndex()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".agent/ORCHESTRATION_CONTRACT.md"] = "# Orchestration Contract\nRules",
            [".agent/CONTEXT_INDEX.md"] = "# Context Index\nOverview",
            [".agent/CODING_STANDARDS.md"] = "# Coding Standards",
            [".agent/TECH_STACK.md"] = "# Tech Stack"
        };

        var gitOps = new FakeGitOperations(files);
        var options = Options.Create(new CodebaseDocumentationOptions());
        var loader = new CodebaseContextLoader(gitOps, options, NullLogger<CodebaseContextLoader>.Instance);

        var context = await loader.LoadRelevantContextAsync("repo", "Sample story", "No keywords");

        var contractIndex = context.IndexOf("<!-- ORCHESTRATION_CONTRACT.md -->", StringComparison.Ordinal);
        var contextIndex = context.IndexOf("<!-- CONTEXT_INDEX.md -->", StringComparison.Ordinal);

        Assert.True(contractIndex >= 0, "Expected ORCHESTRATION_CONTRACT.md section to be included.");
        Assert.True(contextIndex >= 0, "Expected CONTEXT_INDEX.md section to be included.");
        Assert.True(contractIndex < contextIndex, "Expected orchestration contract to be loaded before context index.");
    }

    private sealed class FakeGitOperations : IGitOperations
    {
        private readonly Dictionary<string, string> _files;

        public FakeGitOperations(Dictionary<string, string> files)
        {
            _files = files;
        }

        public Task<string> EnsureBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> EnsureBranchAsync(string branchName, bool lightweightCheckout, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task HydrateWorkingTreeAsync(string repositoryPath, IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CommitAndPushAsync(string repositoryPath, string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task WriteFileAsync(string repositoryPath, string relativePath, string content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string?> ReadFileAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default)
        {
            var normalized = relativePath.Replace('\\', '/');
            _files.TryGetValue(normalized, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task<IReadOnlyList<string>> ListFilesAsync(string repositoryPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetChangedFilesAsync(string repositoryPath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CleanupRepoAsync(string repositoryPath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}