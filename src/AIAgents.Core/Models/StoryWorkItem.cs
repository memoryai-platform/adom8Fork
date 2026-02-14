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
}
