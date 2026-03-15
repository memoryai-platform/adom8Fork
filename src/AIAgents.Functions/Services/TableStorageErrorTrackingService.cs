using AIAgents.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Services;

/// <summary>
/// Azure Table Storage-backed implementation of <see cref="IErrorTrackingService"/>.
/// Stores tracked errors and the last successful PluginTraceLog watermark.
/// </summary>
public sealed class TableStorageErrorTrackingService : IErrorTrackingService
{
    private const string TableName = "ErrorTracking";
    private const string MetadataPartitionKey = "meta";
    private const string WatermarkRowKey = "plugintracelog-watermark";

    private readonly IErrorTrackingTableClient _tableClient;
    private readonly ILogger<TableStorageErrorTrackingService> _logger;

    public TableStorageErrorTrackingService(
        IConfiguration configuration,
        ILogger<TableStorageErrorTrackingService> logger)
        : this(
            new TableClientErrorTrackingTableClient(
                new TableClient(
                    configuration["AzureWebJobsStorage"]
                    ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is required."),
                    TableName)),
            logger)
    {
    }

    internal TableStorageErrorTrackingService(
        IErrorTrackingTableClient tableClient,
        ILogger<TableStorageErrorTrackingService> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    public async Task<ErrorTrackingRecord?> GetAsync(
        string pluginType,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        var response = await _tableClient.GetEntityIfExistsAsync(
            NormalizePartitionKey(pluginType),
            fingerprint,
            cancellationToken);

        return response is not null ? FromEntity(response) : null;
    }

    public async Task<ErrorTrackingRecord> UpsertAsync(
        ErrorTrackingRecord record,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = ToEntity(record);
        var targetEtag = expectedEtag ?? record.ETag;

        if (targetEtag == default || targetEtag == ETag.All)
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
            var created = await GetAsync(record.PluginType, record.Fingerprint, cancellationToken);
            return created ?? record;
        }

        return await UpdateWithOptimisticConcurrencyAsync(record, targetEtag, cancellationToken);
    }

    public async Task<DateTime?> GetWatermarkAsync(CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        var response = await _tableClient.GetEntityIfExistsAsync(
            MetadataPartitionKey,
            WatermarkRowKey,
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        return ParseDateTime(response, "WatermarkUtc");
    }

    public async Task SetWatermarkAsync(
        DateTime watermarkUtc,
        ETag? expectedEtag = null,
        CancellationToken cancellationToken = default)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity(MetadataPartitionKey, WatermarkRowKey)
        {
            ["WatermarkUtc"] = new DateTimeOffset(DateTime.SpecifyKind(watermarkUtc, DateTimeKind.Utc))
        };

        if (expectedEtag == default || expectedEtag is null || expectedEtag == ETag.All)
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
            return;
        }

        await _tableClient.UpdateEntityAsync(entity, expectedEtag.Value, TableUpdateMode.Replace, cancellationToken);
    }

    private async Task<ErrorTrackingRecord> UpdateWithOptimisticConcurrencyAsync(
        ErrorTrackingRecord record,
        ETag expectedEtag,
        CancellationToken cancellationToken)
    {
        var currentRecord = record;
        var currentEtag = expectedEtag;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var entity = ToEntity(currentRecord);

            try
            {
                await _tableClient.UpdateEntityAsync(entity, currentEtag, TableUpdateMode.Replace, cancellationToken);
                currentRecord.ETag = entity.ETag;
                return await GetAsync(currentRecord.PluginType, currentRecord.Fingerprint, cancellationToken) ?? currentRecord;
            }
            catch (RequestFailedException ex) when (ex.Status == 412 && attempt < 3)
            {
                _logger.LogWarning(
                    ex,
                    "Optimistic concurrency conflict while updating tracked error {PluginType}/{Fingerprint}; reloading and retrying (attempt {Attempt}/3).",
                    currentRecord.PluginType,
                    currentRecord.Fingerprint,
                    attempt);

                var existing = await GetAsync(currentRecord.PluginType, currentRecord.Fingerprint, cancellationToken);
                if (existing is null)
                {
                    throw;
                }

                currentRecord = Merge(currentRecord, existing);
                currentEtag = existing.ETag;
            }
        }

        throw new InvalidOperationException("ErrorTracking optimistic concurrency retry loop exited unexpectedly.");
    }

    private static ErrorTrackingRecord Merge(ErrorTrackingRecord attempted, ErrorTrackingRecord current)
    {
        return new ErrorTrackingRecord
        {
            PluginType = current.PluginType,
            MessageName = string.IsNullOrWhiteSpace(attempted.MessageName) ? current.MessageName : attempted.MessageName,
            PrimaryEntity = string.IsNullOrWhiteSpace(attempted.PrimaryEntity) ? current.PrimaryEntity : attempted.PrimaryEntity,
            Fingerprint = current.Fingerprint,
            NormalizedMessage = string.IsNullOrWhiteSpace(attempted.NormalizedMessage) ? current.NormalizedMessage : attempted.NormalizedMessage,
            OccurrenceCount = Math.Max(current.OccurrenceCount, attempted.OccurrenceCount),
            FirstSeenUtc = attempted.FirstSeenUtc == default
                ? current.FirstSeenUtc
                : current.FirstSeenUtc == default
                    ? attempted.FirstSeenUtc
                    : attempted.FirstSeenUtc < current.FirstSeenUtc ? attempted.FirstSeenUtc : current.FirstSeenUtc,
            LastSeenUtc = attempted.LastSeenUtc > current.LastSeenUtc ? attempted.LastSeenUtc : current.LastSeenUtc,
            Status = attempted.Status ?? current.Status,
            Classification = attempted.Classification ?? current.Classification,
            WorkItemId = attempted.WorkItemId ?? current.WorkItemId,
            SuppressedUntilUtc = attempted.SuppressedUntilUtc ?? current.SuppressedUntilUtc,
            ResolvedAtUtc = attempted.ResolvedAtUtc ?? current.ResolvedAtUtc,
            ConsecutiveMissedWindows = Math.Max(attempted.ConsecutiveMissedWindows, current.ConsecutiveMissedWindows),
            ETag = current.ETag
        };
    }

    private static TableEntity ToEntity(ErrorTrackingRecord record)
    {
        var entity = new TableEntity(
            NormalizePartitionKey(record.PluginType),
            record.Fingerprint)
        {
            ["PluginType"] = record.PluginType,
            ["MessageName"] = record.MessageName,
            ["PrimaryEntity"] = record.PrimaryEntity,
            ["Fingerprint"] = record.Fingerprint,
            ["NormalizedMessage"] = record.NormalizedMessage,
            ["OccurrenceCount"] = record.OccurrenceCount,
            ["FirstSeenUtc"] = new DateTimeOffset(DateTime.SpecifyKind(record.FirstSeenUtc, DateTimeKind.Utc)),
            ["LastSeenUtc"] = new DateTimeOffset(DateTime.SpecifyKind(record.LastSeenUtc, DateTimeKind.Utc)),
            ["Status"] = record.Status ?? string.Empty,
            ["Classification"] = record.Classification ?? string.Empty,
            ["WorkItemId"] = record.WorkItemId ?? 0,
            ["SuppressedUntilUtc"] = record.SuppressedUntilUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(record.SuppressedUntilUtc.Value, DateTimeKind.Utc))
                : null,
            ["ResolvedAtUtc"] = record.ResolvedAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(record.ResolvedAtUtc.Value, DateTimeKind.Utc))
                : null,
            ["ConsecutiveMissedWindows"] = record.ConsecutiveMissedWindows
        };

        if (record.ETag != default && record.ETag != ETag.All)
        {
            entity.ETag = record.ETag;
        }

        return entity;
    }

    private static ErrorTrackingRecord FromEntity(TableEntity entity)
    {
        var workItemId = entity.GetInt32("WorkItemId");

        return new ErrorTrackingRecord
        {
            PluginType = entity.GetString("PluginType") ?? string.Empty,
            MessageName = entity.GetString("MessageName") ?? string.Empty,
            PrimaryEntity = entity.GetString("PrimaryEntity") ?? string.Empty,
            Fingerprint = entity.GetString("Fingerprint") ?? entity.RowKey,
            NormalizedMessage = entity.GetString("NormalizedMessage") ?? string.Empty,
            OccurrenceCount = entity.GetInt32("OccurrenceCount") ?? 0,
            FirstSeenUtc = ParseDateTime(entity, "FirstSeenUtc") ?? DateTime.MinValue,
            LastSeenUtc = ParseDateTime(entity, "LastSeenUtc") ?? DateTime.MinValue,
            Status = NullIfEmpty(entity.GetString("Status")),
            Classification = NullIfEmpty(entity.GetString("Classification")),
            WorkItemId = workItemId is > 0 ? workItemId : null,
            SuppressedUntilUtc = ParseDateTime(entity, "SuppressedUntilUtc"),
            ResolvedAtUtc = ParseDateTime(entity, "ResolvedAtUtc"),
            ConsecutiveMissedWindows = entity.GetInt32("ConsecutiveMissedWindows") ?? 0,
            ETag = entity.ETag
        };
    }

    private static string NormalizePartitionKey(string pluginType)
        => pluginType.Trim().ToLowerInvariant();

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTime? ParseDateTime(TableEntity entity, string propertyName)
    {
        try
        {
            var dto = entity.GetDateTimeOffset(propertyName);
            if (dto.HasValue)
            {
                return dto.Value.UtcDateTime;
            }
        }
        catch (InvalidOperationException)
        {
            // Property exists but was stored as a different type. Fall through to string parsing.
        }

        var textValue = entity.GetString(propertyName);
        if (!string.IsNullOrWhiteSpace(textValue) && DateTime.TryParse(textValue, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }
}

internal interface IErrorTrackingTableClient
{
    Task CreateIfNotExistsAsync(CancellationToken cancellationToken);

    Task<TableEntity?> GetEntityIfExistsAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken);

    Task UpsertEntityAsync(
        TableEntity entity,
        TableUpdateMode updateMode,
        CancellationToken cancellationToken);

    Task UpdateEntityAsync(
        TableEntity entity,
        ETag ifMatch,
        TableUpdateMode updateMode,
        CancellationToken cancellationToken);
}

internal sealed class TableClientErrorTrackingTableClient : IErrorTrackingTableClient
{
    private readonly TableClient _tableClient;

    public TableClientErrorTrackingTableClient(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    public async Task CreateIfNotExistsAsync(CancellationToken cancellationToken)
        => await _tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    public async Task<TableEntity?> GetEntityIfExistsAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        var response = await _tableClient.GetEntityIfExistsAsync<TableEntity>(
            partitionKey,
            rowKey,
            cancellationToken: cancellationToken);

        return response.HasValue ? response.Value : null;
    }

    public async Task UpsertEntityAsync(
        TableEntity entity,
        TableUpdateMode updateMode,
        CancellationToken cancellationToken)
        => await _tableClient.UpsertEntityAsync(entity, updateMode, cancellationToken);

    public async Task UpdateEntityAsync(
        TableEntity entity,
        ETag ifMatch,
        TableUpdateMode updateMode,
        CancellationToken cancellationToken)
        => await _tableClient.UpdateEntityAsync(entity, ifMatch, updateMode, cancellationToken);
}
