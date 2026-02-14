namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration options for the AI completion provider.
/// Bound to the "AI" configuration section.
/// </summary>
public sealed class AIOptions
{
    public const string SectionName = "AI";

    /// <summary>
    /// The AI provider to use (e.g., "AzureOpenAI", "OpenAI").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// The model deployment name or identifier.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// The endpoint URL for the AI service.
    /// Required for Azure OpenAI; optional for OpenAI (uses default).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate per completion request.
    /// </summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>
    /// Temperature for sampling (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public double Temperature { get; init; } = 0.3;
}
