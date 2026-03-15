namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration options for Dataverse monitoring.
/// Bound to the "Dataverse" configuration section.
/// </summary>
public sealed class DataverseOptions
{
    public const string SectionName = "Dataverse";

    /// <summary>
    /// The Dataverse organization root URL or API URL.
    /// Examples: https://org.crm.dynamics.com or https://org.crm.dynamics.com/api/data/v9.2
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Azure AD tenant ID for the Dataverse app registration.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Client ID for the Dataverse app registration.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Client secret for the Dataverse app registration.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Timer schedule for the Dataverse monitor.
    /// Phase 1 only introduces the setting; the timer trigger is added later.
    /// </summary>
    public string MonitorSchedule { get; init; } = "0 */15 * * * *";

    /// <summary>
    /// Maximum number of PluginTraceLog rows to request per page.
    /// </summary>
    public int PluginTraceLogPageSize { get; init; } = 250;

    /// <summary>
    /// Returns true when all required Dataverse connection values are present.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}
