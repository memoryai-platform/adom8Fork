using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Services;

public sealed class TableMergeEventDeduplicationStore : IMergeEventDeduplicationStore
{
    private const string TableName = "MergeEventDedup";
    private const string PartitionKey = "pr-merge";

    private readonly TableClient _tableClient;
    private readonly ILogger<TableMergeEventDeduplicationStore> _logger;

    public TableMergeEventDeduplicationStore(IConfiguration configuration, ILogger<TableMergeEventDeduplicationStore> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is required.");

        _tableClient = new TableClient(connectionString, TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<bool> TryProcessAsync(string dedupeKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _tableClient.AddEntityAsync(new TableEntity(PartitionKey, dedupeKey)
            {
                ["Processed"] = false,
                ["UpdatedAt"] = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Entity already exists; proceed to CAS claim attempt below.
        }

        var current = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, dedupeKey, cancellationToken: cancellationToken);
        var entity = current.Value;
        if (entity.GetBoolean("Processed") == true)
        {
            return false;
        }

        entity["Processed"] = true;
        entity["UpdatedAt"] = DateTimeOffset.UtcNow;

        try
        {
            await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogInformation("Suppressed duplicate merge event (ETag CAS conflict) for {DedupeKey}", dedupeKey);
            return false;
        }
    }
}
