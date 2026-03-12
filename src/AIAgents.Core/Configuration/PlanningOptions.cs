namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration for Planning agent workflow behavior.
/// Bound to the "Planning" configuration section.
/// </summary>
public sealed class PlanningOptions
{
    public const string SectionName = "Planning";

    /// <summary>
    /// Complexity threshold above which a User Story should be promoted to a Feature.
    /// </summary>
    public int FeaturePromotionComplexityThreshold { get; init; } = 13;

    /// <summary>
    /// Sub-task threshold above which a User Story should be promoted to a Feature.
    /// </summary>
    public int FeaturePromotionSubstoryThreshold { get; init; } = 8;
}
