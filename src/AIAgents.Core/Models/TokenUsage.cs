using System.Text.Json.Serialization;

namespace AIAgents.Core.Models;

/// <summary>
/// Token usage data from a single AI API call.
/// </summary>
public sealed class TokenUsageData
{
    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("estimatedCost")]
    public decimal EstimatedCost { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
}

/// <summary>
/// Accumulated token usage for a single agent across all its AI calls.
/// </summary>
public sealed class AgentTokenUsage
{
    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("estimatedCost")]
    public decimal EstimatedCost { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("callCount")]
    public int CallCount { get; set; }
}

/// <summary>
/// Aggregated token usage for an entire story across all agents.
/// Stored in state.json alongside agent status and artifacts.
/// </summary>
public sealed class StoryTokenUsage
{
    [JsonPropertyName("totalInputTokens")]
    public int TotalInputTokens { get; set; }

    [JsonPropertyName("totalOutputTokens")]
    public int TotalOutputTokens { get; set; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("complexity")]
    public string Complexity { get; set; } = "XS";

    [JsonPropertyName("agents")]
    public Dictionary<string, AgentTokenUsage> Agents { get; set; } = new();

    /// <summary>
    /// Records usage from a single AI completion call for the given agent.
    /// Accumulates totals and recalculates complexity.
    /// </summary>
    public void RecordUsage(string agentName, TokenUsageData? usage)
    {
        if (usage is null) return;

        if (!Agents.TryGetValue(agentName, out var agentUsage))
        {
            agentUsage = new AgentTokenUsage { Model = usage.Model };
            Agents[agentName] = agentUsage;
        }

        agentUsage.InputTokens += usage.InputTokens;
        agentUsage.OutputTokens += usage.OutputTokens;
        agentUsage.TotalTokens += usage.TotalTokens;
        agentUsage.EstimatedCost += usage.EstimatedCost;
        agentUsage.CallCount++;
        agentUsage.Model = usage.Model; // last model used

        TotalInputTokens += usage.InputTokens;
        TotalOutputTokens += usage.OutputTokens;
        TotalTokens += usage.TotalTokens;
        TotalCost += usage.EstimatedCost;

        Complexity = ClassifyComplexity(TotalTokens);
    }

    /// <summary>
    /// Classifies story complexity based on total token usage.
    /// </summary>
    public static string ClassifyComplexity(int totalTokens) => totalTokens switch
    {
        < 5_000 => "XS",    // Simple bug fix
        < 15_000 => "S",    // Small feature
        < 30_000 => "M",    // Medium feature
        < 60_000 => "L",    // Large feature
        _ => "XL"           // Complex refactor
    };
}

/// <summary>
/// Calculates estimated cost based on model pricing.
/// Contains default pricing for common models. Falls back to GPT-4o pricing.
/// </summary>
public static class TokenCostCalculator
{
    // (inputPricePerMillionTokens, outputPricePerMillionTokens)
    private static readonly Dictionary<string, (decimal input, decimal output)> s_pricing =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI
            ["gpt-4o"] = (2.50m, 10.00m),
            ["gpt-4o-mini"] = (0.15m, 0.60m),
            ["gpt-4-turbo"] = (10.00m, 30.00m),
            ["gpt-4"] = (30.00m, 60.00m),
            ["gpt-3.5-turbo"] = (0.50m, 1.50m),
            ["o1-mini"] = (3.00m, 12.00m),
            ["o1"] = (15.00m, 60.00m),
            // Anthropic
            ["claude-sonnet-4-20250514"] = (3.00m, 15.00m),
            ["claude-3-5-sonnet"] = (3.00m, 15.00m),
            ["claude-3-5-sonnet-20241022"] = (3.00m, 15.00m),
            ["claude-3-opus"] = (15.00m, 75.00m),
            ["claude-3-opus-20240229"] = (15.00m, 75.00m),
            ["claude-3-haiku"] = (0.25m, 1.25m),
            ["claude-3-5-haiku"] = (0.80m, 4.00m),
        };

    /// <summary>
    /// Calculates estimated cost for a given model and token counts.
    /// Returns 0 if token counts are zero.
    /// </summary>
    public static decimal Calculate(string model, int inputTokens, int outputTokens)
    {
        if (inputTokens == 0 && outputTokens == 0) return 0m;

        var (inputRate, outputRate) = s_pricing.TryGetValue(model, out var rates)
            ? rates
            : (2.50m, 10.00m); // Default fallback (GPT-4o pricing)

        return (inputTokens * inputRate / 1_000_000m) + (outputTokens * outputRate / 1_000_000m);
    }
}
