using AIAgents.Core.Models;

namespace AIAgents.Core.Interfaces;

/// <summary>
/// Provides read/write access to a story's working directory and state.
/// Each story gets its own directory under .ado/stories/US-{id}/.
/// </summary>
public interface IStoryContext : IAsyncDisposable
{
    /// <summary>
    /// The work item ID this context is associated with.
    /// </summary>
    int WorkItemId { get; }

    /// <summary>
    /// The absolute path to the story's working directory.
    /// </summary>
    string StoryDirectory { get; }

    /// <summary>
    /// Loads the current story state from state.json.
    /// Creates a default state if the file does not exist.
    /// </summary>
    Task<StoryState> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the story state to state.json atomically (temp file + move).
    /// </summary>
    Task SaveStateAsync(StoryState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an artifact file to the story directory.
    /// </summary>
    /// <param name="relativePath">Path relative to the story directory (e.g., "PLAN.md").</param>
    /// <param name="content">File content.</param>
    Task WriteArtifactAsync(string relativePath, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an artifact file from the story directory.
    /// </summary>
    /// <param name="relativePath">Path relative to the story directory.</param>
    /// <returns>File content, or null if the file does not exist.</returns>
    Task<string?> ReadArtifactAsync(string relativePath, CancellationToken cancellationToken = default);
}
