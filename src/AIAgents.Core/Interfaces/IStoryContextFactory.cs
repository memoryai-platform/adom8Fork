namespace AIAgents.Core.Interfaces;

/// <summary>
/// Factory for creating <see cref="IStoryContext"/> instances.
/// </summary>
public interface IStoryContextFactory
{
    /// <summary>
    /// Creates a story context for the given work item ID.
    /// Ensures the story directory exists.
    /// </summary>
    /// <param name="workItemId">The Azure DevOps work item ID.</param>
    /// <param name="repositoryPath">The local repository root path.</param>
    /// <returns>A new <see cref="IStoryContext"/> instance.</returns>
    IStoryContext Create(int workItemId, string repositoryPath);
}
