namespace AIAgents.Core.Models;

/// <summary>
/// Result of an AI code review, parsed from the AI response.
/// </summary>
public sealed record CodeReviewResult
{
    public required int Score { get; init; }
    public required string Recommendation { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<ReviewIssue> CriticalIssues { get; init; }
    public required IReadOnlyList<ReviewIssue> HighIssues { get; init; }
    public required IReadOnlyList<ReviewIssue> MediumIssues { get; init; }
    public required IReadOnlyList<ReviewIssue> LowIssues { get; init; }
    public required IReadOnlyList<string> PositiveFindings { get; init; }
}

/// <summary>
/// A single issue identified during code review.
/// </summary>
public sealed record ReviewIssue
{
    public int? Line { get; init; }
    public required string Issue { get; init; }
    public string? Fix { get; init; }
    public string? Code { get; init; }
}
