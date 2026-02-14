namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration for input validation and security checks.
/// Bound to the "InputValidation" configuration section.
/// </summary>
public sealed class InputValidationOptions
{
    public const string SectionName = "InputValidation";

    /// <summary>Maximum allowed length for work item title. Default: 255.</summary>
    public int MaxTitleLength { get; set; } = 255;

    /// <summary>Maximum allowed length for work item description. Default: 10,000.</summary>
    public int MaxDescriptionLength { get; set; } = 10_000;

    /// <summary>Maximum allowed length for acceptance criteria. Default: 5,000.</summary>
    public int MaxAcceptanceCriteriaLength { get; set; } = 5_000;

    /// <summary>Whether to detect common prompt injection patterns. Default: true.</summary>
    public bool EnablePromptInjectionDetection { get; set; } = true;

    /// <summary>Whether to detect and reject HTML/script tags. Default: true.</summary>
    public bool EnableHtmlSanitization { get; set; } = true;

    /// <summary>Maximum work items from same source within one hour. Default: 20.</summary>
    public int RateLimitPerHour { get; set; } = 20;

    /// <summary>
    /// When true, prompt injection detections are errors (block processing).
    /// When false, they are warnings (log and continue). Default: false.
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>Maximum AI cost per story in USD before warning. Default: $5.00.</summary>
    public decimal MaxCostPerStory { get; set; } = 5.00m;
}
