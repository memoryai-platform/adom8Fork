namespace AIAgents.Core.Models;

/// <summary>
/// Result of AI planning analysis, parsed from the AI response.
/// </summary>
public sealed record PlanningResult
{
    public required string ProblemAnalysis { get; init; }
    public required string TechnicalApproach { get; init; }
    public required IReadOnlyList<string> AffectedFiles { get; init; }
    public required int Complexity { get; init; }
    public required string Architecture { get; init; }
    public required IReadOnlyList<string> SubTasks { get; init; }
    public required IReadOnlyList<string> Dependencies { get; init; }
    public required IReadOnlyList<string> Risks { get; init; }
    public required IReadOnlyList<string> Assumptions { get; init; }
    public required string TestingStrategy { get; init; }
}
