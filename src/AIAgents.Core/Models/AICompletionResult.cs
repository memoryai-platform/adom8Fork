namespace AIAgents.Core.Models;

/// <summary>
/// Result of an AI completion request, containing both the generated text
/// and optional token usage metadata for cost tracking.
/// </summary>
public sealed class AICompletionResult
{
    /// <summary>The AI-generated completion text.</summary>
    public required string Content { get; init; }

    /// <summary>Token usage data from the API response. Null if the provider didn't return usage info.</summary>
    public TokenUsageData? Usage { get; init; }

    /// <summary>
    /// Implicit conversion to string for backward-compatible call sites.
    /// </summary>
    public static implicit operator string(AICompletionResult result) => result.Content;
}
