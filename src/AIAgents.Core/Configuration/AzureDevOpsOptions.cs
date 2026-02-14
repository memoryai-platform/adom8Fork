namespace AIAgents.Core.Configuration;

/// <summary>
/// Configuration options for Azure DevOps connectivity.
/// Bound to the "AzureDevOps" configuration section.
/// </summary>
public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// The Azure DevOps organization URL (e.g., https://dev.azure.com/myorg).
    /// </summary>
    public required string OrganizationUrl { get; init; }

    /// <summary>
    /// Personal Access Token for authentication.
    /// Must have Work Items (Read/Write), Code (Read/Write), and Build (Read) scopes.
    /// </summary>
    public required string Pat { get; init; }

    /// <summary>
    /// The Azure DevOps project name.
    /// </summary>
    public required string Project { get; init; }
}
