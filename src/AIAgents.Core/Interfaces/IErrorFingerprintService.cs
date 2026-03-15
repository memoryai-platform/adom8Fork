namespace AIAgents.Core.Interfaces;

/// <summary>
/// Normalizes plugin error payloads and computes deterministic fingerprints.
/// </summary>
public interface IErrorFingerprintService
{
    string Normalize(
        string? pluginType,
        string? messageName,
        string? primaryEntity,
        string? errorText);

    string ComputeFingerprint(
        string? pluginType,
        string? messageName,
        string? primaryEntity,
        string? errorText);
}
