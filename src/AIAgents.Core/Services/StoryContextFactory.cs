using AIAgents.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIAgents.Core.Services;

/// <summary>
/// Factory for creating <see cref="StoryContext"/> instances.
/// </summary>
public sealed class StoryContextFactory : IStoryContextFactory
{
    private readonly ILogger<StoryContext> _logger;

    public StoryContextFactory(ILogger<StoryContext> logger)
    {
        _logger = logger;
    }

    public IStoryContext Create(int workItemId, string repositoryPath)
    {
        return new StoryContext(workItemId, repositoryPath, _logger);
    }
}
