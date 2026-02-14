using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Core.Services;

/// <summary>
/// Thin AI completion client that handles HTTP transport to OpenAI-compatible endpoints.
/// All prompt engineering is owned by the agent services, not by this client.
/// </summary>
public sealed class AIClient : IAIClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AIOptions _options;
    private readonly ILogger<AIClient> _logger;

    public AIClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AIOptions> options,
        ILogger<AIClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        AICompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("AIClient");

        var requestBody = BuildRequestBody(systemPrompt, userPrompt, options);

        _logger.LogDebug(
            "Sending completion request to {Provider} model {Model}, max_tokens={MaxTokens}",
            _options.Provider, _options.Model, options?.MaxTokens ?? _options.MaxTokens);

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            GetCompletionEndpoint(),
            jsonContent,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

        var content = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("AI completion returned empty content");
            throw new InvalidOperationException("AI completion returned empty content.");
        }

        _logger.LogDebug(
            "Completion received: {TokenCount} characters",
            content.Length);

        return content;
    }

    private object BuildRequestBody(string systemPrompt, string userPrompt, AICompletionOptions? options)
    {
        return new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = options?.MaxTokens ?? _options.MaxTokens,
            temperature = options?.Temperature ?? _options.Temperature
        };
    }

    private string GetCompletionEndpoint()
    {
        return _options.Provider.ToUpperInvariant() switch
        {
            "AZUREOPENAI" => $"openai/deployments/{_options.Model}/chat/completions?api-version=2024-08-01-preview",
            "OPENAI" => "v1/chat/completions",
            _ => throw new NotSupportedException($"AI provider '{_options.Provider}' is not supported.")
        };
    }

    // Internal response DTOs for deserializing OpenAI-compatible responses
    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public MessageContent? Message { get; init; }
    }

    private sealed class MessageContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
