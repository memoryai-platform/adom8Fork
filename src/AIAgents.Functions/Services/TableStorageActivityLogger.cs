using AIAgents.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgents.Functions.Services;

/// <summary>
/// Stores activity log entries in Azure Table Storage for fast dashboard reads.
/// Uses an inverted timestamp as RowKey to enable reverse-chronological queries.
/// </summary>
public sealed class TableStorageActivityLogger : IActivityLogger
{
    private const string TableName = "AgentActivity";
    private const string PartitionKey = "activity";

    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageActivityLogger> _logger;

    public TableStorageActivityLogger(
        IConfiguration configuration,
        ILogger<TableStorageActivityLogger> logger)
    {
        _logger = logger;

        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is required.");

        _tableClient = new TableClient(connectionString, TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task LogAsync(
        string agent,
        int workItemId,
        string message,
        string level = "info",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Inverted tick count for reverse-chronological ordering
        var invertedTicks = (DateTime.MaxValue.Ticks - now.Ticks).ToString("D20");

        var entity = new TableEntity(PartitionKey, invertedTicks)
        {
            ["Agent"] = agent,
            ["WorkItemId"] = workItemId,
            ["Message"] = message,
            ["Level"] = level,
            ["Timestamp_Utc"] = now.ToString("O")
        };

        try
        {
            await _tableClient.AddEntityAsync(entity, cancellationToken);
            _logger.LogDebug("Logged activity: [{Agent}] {Message} for WI-{WorkItemId}",
                agent, message, workItemId);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed to log activity to Table Storage");
        }
    }

    public async Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<ActivityEntry>();

        // Query with partition key filter; rows are already in reverse-chronological order
        var query = _tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{PartitionKey}'",
            maxPerPage: count,
            cancellationToken: cancellationToken);

        var taken = 0;
        await foreach (var entity in query)
        {
            if (taken >= count) break;

            entries.Add(new ActivityEntry
            {
                Timestamp = DateTime.TryParse(entity.GetString("Timestamp_Utc"), out var ts)
                    ? ts : DateTime.UtcNow,
                Agent = entity.GetString("Agent") ?? "Unknown",
                WorkItemId = entity.GetInt32("WorkItemId") ?? 0,
                Message = entity.GetString("Message") ?? string.Empty,
                Level = entity.GetString("Level") ?? "info"
            });

            taken++;
        }

        return entries;
    }
}
