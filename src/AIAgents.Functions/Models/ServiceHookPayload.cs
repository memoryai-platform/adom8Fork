using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIAgents.Functions.Models;

/// <summary>
/// Payload received from an Azure DevOps Service Hook (work item updated).
/// Only the fields we need are modeled; the rest is captured in RawJson.
/// </summary>
public sealed class ServiceHookPayload
{
    [JsonPropertyName("eventType")]
    public string? EventType { get; init; }

    [JsonPropertyName("resource")]
    public ServiceHookResource? Resource { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// The resource section of a Service Hook payload containing the work item data.
/// </summary>
public sealed class ServiceHookResource
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("workItemId")]
    public int WorkItemId { get; init; }

    [JsonPropertyName("fields")]
    public ServiceHookFields? Fields { get; init; }

    [JsonPropertyName("revision")]
    public ServiceHookRevision? Revision { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Changed fields in the Service Hook payload.
/// </summary>
public sealed class ServiceHookFields
{
    [JsonPropertyName("System.State")]
    public FieldChange? State { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Represents a field change with old and new values.
/// </summary>
public sealed class FieldChange
{
    [JsonPropertyName("oldValue")]
    public string? OldValue { get; init; }

    [JsonPropertyName("newValue")]
    public string? NewValue { get; init; }
}

/// <summary>
/// The revision section of a Service Hook resource, containing the full work item.
/// </summary>
public sealed class ServiceHookRevision
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("fields")]
    public Dictionary<string, JsonElement>? Fields { get; init; }
}
