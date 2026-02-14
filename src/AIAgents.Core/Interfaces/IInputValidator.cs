using AIAgents.Core.Models;

namespace AIAgents.Core.Interfaces;

/// <summary>
/// Validates work item content before queuing for agent processing.
/// Checks length limits, content restrictions, and prompt injection patterns.
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validates the given work item's content (title, description, acceptance criteria)
    /// against configured rules including length limits, HTML sanitization, and prompt injection detection.
    /// </summary>
    /// <param name="workItem">The work item to validate.</param>
    /// <returns>Validation result containing any errors and warnings.</returns>
    ValidationResult ValidateWorkItem(StoryWorkItem workItem);
}
