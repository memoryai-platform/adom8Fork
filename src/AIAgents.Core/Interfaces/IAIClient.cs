namespace AIAgents.Core.Interfaces;

/// <summary>
/// Thin AI completion client. Agents own all prompt engineering;
/// this interface only handles the API transport.
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Sends a completion request to the configured AI provider.
    /// </summary>
    /// <param name="systemPrompt">The system-level instruction prompt.</param>
    /// <param name="userPrompt">The user-level content prompt.</param>
    /// <param name="options">Optional overrides for temperature, max tokens, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI-generated completion text.</returns>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        AICompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-request overrides for AI completion behavior.
/// </summary>
public sealed class AICompletionOptions
{
    /// <summary>
    /// Override the default maximum tokens for this request.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Override the default temperature for this request.
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Optional JSON schema name hint for structured output.
    /// </summary>
    public string? ResponseFormat { get; init; }
}
