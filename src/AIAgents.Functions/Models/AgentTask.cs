using System.Text.Json.Serialization;

namespace AIAgents.Functions.Models;

/// <summary>
/// Message placed on the agent-tasks queue to trigger agent execution.
/// </summary>
public sealed record AgentTask
{
    /// <summary>
    /// The Azure DevOps work item ID of the story to process.
    /// </summary>
    [JsonPropertyName("workItemId")]
    public required int WorkItemId { get; init; }

    /// <summary>
    /// Which agent should process this task.
    /// </summary>
    [JsonPropertyName("agentType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required AgentType AgentType { get; init; }

    /// <summary>
    /// Correlation ID for tracing the task through the pipeline.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// When this task was enqueued.
    /// </summary>
    [JsonPropertyName("enqueuedAt")]
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional source that requested this task (e.g., OrchestratorWebhook, ResumePipeline).
    /// </summary>
    [JsonPropertyName("triggerSource")]
    public string? TriggerSource { get; init; }

    /// <summary>
    /// Optional previous stage this task is resuming from.
    /// </summary>
    [JsonPropertyName("resumeFromStage")]
    public string? ResumeFromStage { get; init; }

    /// <summary>
    /// Optional handoff note to help diagnostics and resumability.
    /// </summary>
    [JsonPropertyName("handoffNote")]
    public string? HandoffNote { get; init; }

    /// <summary>
    /// True when the Planning agent was triggered by a user reply/comment while the story was already in Planning.
    /// </summary>
    [JsonPropertyName("isPlanningReplyTrigger")]
    public bool IsPlanningReplyTrigger { get; init; }

    /// <summary>
    /// True when prompt generation should include prior discussion/comment history.
    /// </summary>
    [JsonPropertyName("includeCommentHistory")]
    public bool IncludeCommentHistory { get; init; }
}
