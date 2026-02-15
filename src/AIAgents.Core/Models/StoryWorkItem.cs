namespace AIAgents.Core.Models;

/// <summary>
/// Represents an Azure DevOps user story work item.
/// Named StoryWorkItem to avoid collision with Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem.
/// </summary>
public sealed record StoryWorkItem
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? AcceptanceCriteria { get; init; }
    public required string State { get; init; }
    public string? AssignedTo { get; init; }
    public string? AreaPath { get; init; }
    public string? IterationPath { get; init; }
    public int? StoryPoints { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTime CreatedDate { get; init; }
    public DateTime ChangedDate { get; init; }

    /// <summary>
    /// AI autonomy level (1-5). Read from custom field Custom.AIAutonomyLevel.
    /// 1=Plan Only, 2=Code Only, 3=Review &amp; Pause, 4=Auto-Merge, 5=Full Autonomy.
    /// </summary>
    public int AutonomyLevel { get; init; } = 3;

    /// <summary>
    /// Minimum review score for auto-merge/deploy. Read from Custom.AIMinimumReviewScore.
    /// </summary>
    public int MinimumReviewScore { get; init; } = 85;

    // ── AI Output Fields (written by agents, read back for display) ─────

    /// <summary>Total tokens consumed across all agents.</summary>
    public int? AITokensUsed { get; init; }

    /// <summary>Estimated cost string (e.g., "$0.1234").</summary>
    public string? AICost { get; init; }

    /// <summary>Complexity classification: XS, S, M, L, XL.</summary>
    public string? AIComplexity { get; init; }

    /// <summary>Per-agent AI model breakdown (e.g., "Planning: gpt-4o, Coding: claude-opus").</summary>
    public string? AIModel { get; init; }

    /// <summary>Code review score (0–100).</summary>
    public int? AIReviewScore { get; init; }

    /// <summary>Pipeline processing time in seconds.</summary>
    public decimal? AIProcessingTime { get; init; }

    /// <summary>Number of source code files generated.</summary>
    public int? AIFilesGenerated { get; init; }

    /// <summary>Number of test files generated.</summary>
    public int? AITestsGenerated { get; init; }

    /// <summary>Pull request number.</summary>
    public int? AIPRNumber { get; init; }

    /// <summary>Last agent that processed this story.</summary>
    public string? AILastAgent { get; init; }

    /// <summary>Critical issues found during review.</summary>
    public int? AICriticalIssues { get; init; }

    /// <summary>Deployment decision (e.g., "Auto-merged").</summary>
    public string? AIDeploymentDecision { get; init; }
}
