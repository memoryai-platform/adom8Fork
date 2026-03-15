using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIAgents.Core.Interfaces;

namespace AIAgents.Core.Services;

/// <summary>
/// Produces deterministic fingerprints for plugin errors after normalizing variable values.
/// </summary>
public sealed partial class ErrorFingerprintService : IErrorFingerprintService
{
    public string Normalize(
        string? pluginType,
        string? messageName,
        string? primaryEntity,
        string? errorText)
    {
        var normalizedMessage = NormalizeErrorText(errorText);

        return string.Join("|",
            NormalizeComponent(pluginType),
            NormalizeComponent(messageName),
            NormalizeComponent(primaryEntity),
            normalizedMessage);
    }

    public string ComputeFingerprint(
        string? pluginType,
        string? messageName,
        string? primaryEntity,
        string? errorText)
    {
        var normalized = Normalize(pluginType, messageName, primaryEntity, errorText);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeErrorText(string? errorText)
    {
        var value = errorText ?? string.Empty;
        value = GuidRegex().Replace(value, "[guid]");
        value = TimestampRegex().Replace(value, "[timestamp]");
        value = NumericIdRegex().Replace(value, "[id]");
        value = MemoryAddressRegex().Replace(value, "[addr]");
        return CollapseWhitespace(value);
    }

    private static string NormalizeComponent(string? value)
        => CollapseWhitespace(value ?? string.Empty);

    private static string CollapseWhitespace(string value)
        => WhitespaceRegex().Replace(value, " ").Trim().ToLowerInvariant();

    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}(?:[tT ]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:[zZ]|[+-]\d{2}:\d{2})?)?\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"(?<![a-zA-Z0-9])\d{6,}(?![a-zA-Z0-9])")]
    private static partial Regex NumericIdRegex();

    [GeneratedRegex(@"(?i)\b0x[0-9a-f]{6,}\b")]
    private static partial Regex MemoryAddressRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
