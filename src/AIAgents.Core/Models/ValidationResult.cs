namespace AIAgents.Core.Models;

/// <summary>
/// Result of input validation against a work item.
/// Contains any errors (hard blocks) and warnings (informational but processing continues).
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Whether the work item passed all validation rules without errors.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Validation errors that block processing.</summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>Validation warnings that are logged but don't block processing.</summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>Creates a passing validation result with no errors or warnings.</summary>
    public static ValidationResult Valid() => new();
}
