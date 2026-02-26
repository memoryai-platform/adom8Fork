using System.Text.RegularExpressions;
using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIAgents.Core.Services;

/// <summary>
/// Validates work item content before agent processing.
/// Enforces length limits, detects HTML/script injection, SQL injection patterns,
/// and common prompt injection attacks. Configurable via <see cref="InputValidationOptions"/>.
/// </summary>
public sealed class InputValidator : IInputValidator
{
    private readonly InputValidationOptions _options;
    private readonly ILogger<InputValidator> _logger;

    // Prompt injection patterns — case-insensitive
    private static readonly string[] s_promptInjectionPatterns =
    [
        @"ignore\s+(all\s+)?previous\s+instructions",
        @"you\s+are\s+now\s+in\s+developer\s+mode",
        @"disregard\s+(your\s+)?(system\s+)?prompt",
        @"reset\s+your\s+instructions",
        @"forget\s+(all\s+)?previous\s+(instructions|context)",
        @"override\s+(system|safety)\s+(prompt|instructions)",
        @"act\s+as\s+(if\s+)?(you\s+have\s+)?no\s+restrictions",
        @"jailbreak",
        @"DAN\s+mode",
    ];

    private static readonly Regex[] s_injectionRegexes = s_promptInjectionPatterns
        .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    // HTML/script tag pattern
    private static readonly Regex s_htmlTagRegex = new(
        @"<\s*(script|iframe|object|embed)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // SQL injection patterns
    private static readonly Regex s_sqlInjectionRegex = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|EXECUTE|UNION)\b\s+(ALL\s+)?.*\b(FROM|INTO|TABLE|SET|WHERE)\b)" +
        @"|(';\s*(DROP|DELETE|INSERT|UPDATE)\b)" +
        @"|(--\s*$)" +
        @"|(\b(OR|AND)\s+['\d]+=\s*['\d]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InputValidator(IOptions<InputValidationOptions> options, ILogger<InputValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ValidationResult ValidateWorkItem(StoryWorkItem workItem)
    {
        var result = new ValidationResult();

        ValidateLength(workItem, result);

        if (_options.EnableHtmlSanitization)
        {
            ValidateHtmlContent(workItem, result);
        }

        ValidateSqlInjection(workItem, result);

        if (_options.EnablePromptInjectionDetection)
        {
            ValidatePromptInjection(workItem, result);
        }

        ValidateSpecialCharacterRatio(workItem, result);

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Input validation failed for WI-{WorkItemId}: {ErrorCount} errors, {WarningCount} warnings",
                workItem.Id, result.Errors.Count, result.Warnings.Count);
        }
        else if (result.Warnings.Count > 0)
        {
            _logger.LogInformation(
                "Input validation passed with warnings for WI-{WorkItemId}: {WarningCount} warnings",
                workItem.Id, result.Warnings.Count);
        }

        return result;
    }

    private void ValidateLength(StoryWorkItem workItem, ValidationResult result)
    {
        if (workItem.Title.Length > _options.MaxTitleLength)
        {
            result.Errors.Add($"Title exceeds maximum length of {_options.MaxTitleLength} characters (actual: {workItem.Title.Length}).");
        }

        if (workItem.Description?.Length > _options.MaxDescriptionLength)
        {
            result.Errors.Add($"Description exceeds maximum length of {_options.MaxDescriptionLength} characters (actual: {workItem.Description.Length}).");
        }

        if (workItem.AcceptanceCriteria?.Length > _options.MaxAcceptanceCriteriaLength)
        {
            result.Errors.Add($"Acceptance criteria exceeds maximum length of {_options.MaxAcceptanceCriteriaLength} characters (actual: {workItem.AcceptanceCriteria.Length}).");
        }
    }

    private void ValidateHtmlContent(StoryWorkItem workItem, ValidationResult result)
    {
        CheckHtmlInField(workItem.Title, "Title", result);
        CheckHtmlInField(workItem.Description, "Description", result);
        CheckHtmlInField(workItem.AcceptanceCriteria, "Acceptance Criteria", result);
    }

    private void CheckHtmlInField(string? value, string fieldName, ValidationResult result)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (s_htmlTagRegex.IsMatch(value))
        {
            result.Errors.Add($"{fieldName} contains potentially dangerous HTML tags (script, iframe, etc.).");
        }
    }

    private static void ValidateSqlInjection(StoryWorkItem workItem, ValidationResult result)
    {
        CheckSqlInField(workItem.Title, "Title", result);
        CheckSqlInField(workItem.Description, "Description", result);
        CheckSqlInField(workItem.AcceptanceCriteria, "Acceptance Criteria", result);
    }

    private static void CheckSqlInField(string? value, string fieldName, ValidationResult result)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (s_sqlInjectionRegex.IsMatch(value))
        {
            result.Warnings.Add($"{fieldName} contains patterns resembling SQL injection. Content flagged for review.");
        }
    }

    private void ValidatePromptInjection(StoryWorkItem workItem, ValidationResult result)
    {
        CheckInjectionInField(workItem.Title, "Title", result);
        CheckInjectionInField(workItem.Description, "Description", result);
        CheckInjectionInField(workItem.AcceptanceCriteria, "Acceptance Criteria", result);
    }

    private void CheckInjectionInField(string? value, string fieldName, ValidationResult result)
    {
        if (string.IsNullOrEmpty(value)) return;

        foreach (var regex in s_injectionRegexes)
        {
            if (regex.IsMatch(value))
            {
                var message = $"{fieldName} contains a prompt injection pattern: '{regex}'. This is suspicious.";

                if (_options.StrictMode)
                {
                    result.Errors.Add(message);
                }
                else
                {
                    result.Warnings.Add(message);
                }

                break; // One detection per field is enough
            }
        }
    }

    private void ValidateSpecialCharacterRatio(StoryWorkItem workItem, ValidationResult result)
    {
        CheckSpecialCharRatio(workItem.Description, "Description", result);
        CheckSpecialCharRatio(workItem.AcceptanceCriteria, "Acceptance Criteria", result);
    }

    private void CheckSpecialCharRatio(string? value, string fieldName, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 20) return;

        var specialCount = value.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        var ratio = (double)specialCount / value.Length;

        if (ratio > 0.20)
        {
            var message = $"{fieldName} has an unusually high ratio of special characters ({ratio:P0}), which may indicate encoded or obfuscated content.";

            if (_options.StrictMode)
            {
                result.Errors.Add(message);
            }
            else
            {
                result.Warnings.Add(message);
            }
        }
    }
}
