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

public sealed class ServiceHookCommentVersionRef
{
    [JsonPropertyName("commentId")]
    public int CommentId { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public sealed class ServiceHookResource
{
    [JsonPropertyName("commentVersionRef")]
    public ServiceHookCommentVersionRef? CommentVersionRef { get; init; }

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


public static class ServiceHookPayloadExtensions
{
    public static bool IsWorkItemUpdatedEvent(this ServiceHookPayload payload)
        => string.Equals(payload.EventType, "workitem.updated", StringComparison.OrdinalIgnoreCase);

    public static bool IsCommentAddedEvent(this ServiceHookPayload payload)
    {
        if (payload.Resource?.CommentVersionRef?.CommentId > 0)
        {
            return true;
        }

        return string.Equals(payload.EventType, "workitem.commented", StringComparison.OrdinalIgnoreCase)
            || string.Equals(payload.EventType, "ms.vss-work.workitem-commented-event", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetCurrentState(this ServiceHookPayload payload)
    {
        var stateChange = payload.Resource?.Fields?.State;
        if (!string.IsNullOrWhiteSpace(stateChange?.NewValue))
        {
            return stateChange.NewValue;
        }

        if (payload.Resource?.Revision?.Fields is null || !payload.Resource.Revision.Fields.TryGetValue("System.State", out var stateNode))
        {
            return null;
        }

        return stateNode.ValueKind == JsonValueKind.String
            ? stateNode.GetString()
            : null;
    }
}
