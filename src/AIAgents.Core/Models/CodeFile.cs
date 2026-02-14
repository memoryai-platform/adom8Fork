namespace AIAgents.Core.Models;

/// <summary>
/// Represents a generated or modified source code file.
/// </summary>
public sealed record CodeFile
{
    /// <summary>
    /// Relative path within the repository (e.g., "src/Services/MyService.cs").
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The full file content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether this is a new file (true) or modification of existing (false).
    /// </summary>
    public bool IsNew { get; init; } = true;
}
