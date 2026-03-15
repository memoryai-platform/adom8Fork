using AIAgents.Functions.Models;
using AIAgents.Functions.Services;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIAgents.Functions.Tests.Services;

public sealed class TableStorageErrorTrackingServiceTests
{
    [Fact]
    public async Task UpsertAsync_Creates_NewTrackedError()
    {
        var fakeClient = new FakeErrorTrackingTableClient();
        var service = new TableStorageErrorTrackingService(fakeClient, NullLogger<TableStorageErrorTrackingService>.Instance);
        var record = CreateRecord();

        var saved = await service.UpsertAsync(record);
        var reloaded = await service.GetAsync(record.PluginType, record.Fingerprint);

        Assert.NotNull(reloaded);
        Assert.Equal(saved.Fingerprint, reloaded!.Fingerprint);
        Assert.Equal(1, reloaded.OccurrenceCount);
        Assert.Equal("plugin.a", fakeClient.LastUpsertedEntity!.PartitionKey);
    }

    [Fact]
    public async Task UpsertAsync_Updates_ExistingTrackedError()
    {
        var fakeClient = new FakeErrorTrackingTableClient();
        var service = new TableStorageErrorTrackingService(fakeClient, NullLogger<TableStorageErrorTrackingService>.Instance);

        var initial = await service.UpsertAsync(CreateRecord());
        var update = CreateRecord();
        update.OccurrenceCount = 2;
        update.LastSeenUtc = update.LastSeenUtc.AddMinutes(5);

        var saved = await service.UpsertAsync(update, initial.ETag);

        Assert.Equal(2, saved.OccurrenceCount);
        Assert.Equal(update.LastSeenUtc, saved.LastSeenUtc);
    }

    [Fact]
    public async Task GetWatermarkAsync_And_SetWatermarkAsync_RoundTrip()
    {
        var fakeClient = new FakeErrorTrackingTableClient();
        var service = new TableStorageErrorTrackingService(fakeClient, NullLogger<TableStorageErrorTrackingService>.Instance);
        var watermark = DateTime.Parse("2026-03-15T01:02:03Z").ToUniversalTime();

        await service.SetWatermarkAsync(watermark);
        var loaded = await service.GetWatermarkAsync();

        Assert.Equal(watermark, loaded);
    }

    [Fact]
    public async Task UpsertAsync_Retries_When_InitialUpdateHits412()
    {
        var fakeClient = new FakeErrorTrackingTableClient { FailNextUpdateWith412 = true };
        var service = new TableStorageErrorTrackingService(fakeClient, NullLogger<TableStorageErrorTrackingService>.Instance);

        var initial = await service.UpsertAsync(CreateRecord());
        var update = CreateRecord();
        update.OccurrenceCount = 2;
        update.LastSeenUtc = update.LastSeenUtc.AddMinutes(3);

        var saved = await service.UpsertAsync(update, initial.ETag);

        Assert.Equal(2, fakeClient.UpdateAttempts);
        Assert.Equal(2, saved.OccurrenceCount);
        Assert.Equal(update.LastSeenUtc, saved.LastSeenUtc);
    }

    private static ErrorTrackingRecord CreateRecord() => new()
    {
        PluginType = "Plugin.A",
        MessageName = "Create",
        PrimaryEntity = "account",
        Fingerprint = "abc123",
        NormalizedMessage = "boom",
        OccurrenceCount = 1,
        FirstSeenUtc = DateTime.Parse("2026-03-15T01:00:00Z").ToUniversalTime(),
        LastSeenUtc = DateTime.Parse("2026-03-15T01:00:00Z").ToUniversalTime(),
        Status = "Active",
        Classification = "Unknown",
        ConsecutiveMissedWindows = 0
    };

    private sealed class FakeErrorTrackingTableClient : IErrorTrackingTableClient
    {
        private readonly Dictionary<(string PartitionKey, string RowKey), TableEntity> _store = new();
        private int _etagCounter = 1;

        public bool FailNextUpdateWith412 { get; set; }
        public int UpdateAttempts { get; private set; }
        public TableEntity? LastUpsertedEntity { get; private set; }

        public Task CreateIfNotExistsAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<TableEntity?> GetEntityIfExistsAsync(
            string partitionKey,
            string rowKey,
            CancellationToken cancellationToken)
        {
            if (_store.TryGetValue((partitionKey, rowKey), out var entity))
            {
                return Task.FromResult<TableEntity?>(Clone(entity));
            }

            return Task.FromResult<TableEntity?>(null);
        }

        public Task UpsertEntityAsync(
            TableEntity entity,
            TableUpdateMode updateMode,
            CancellationToken cancellationToken)
        {
            LastUpsertedEntity = Clone(entity);
            var persisted = Clone(entity);
            persisted.ETag = NextEtag();
            _store[(persisted.PartitionKey, persisted.RowKey)] = persisted;
            return Task.CompletedTask;
        }

        public Task UpdateEntityAsync(
            TableEntity entity,
            ETag ifMatch,
            TableUpdateMode updateMode,
            CancellationToken cancellationToken)
        {
            UpdateAttempts++;

            if (!_store.TryGetValue((entity.PartitionKey, entity.RowKey), out var current))
            {
                throw new RequestFailedException(404, "Entity not found.");
            }

            if (FailNextUpdateWith412)
            {
                FailNextUpdateWith412 = false;
                throw new RequestFailedException(412, "Precondition failed.");
            }

            if (current.ETag != ifMatch)
            {
                throw new RequestFailedException(412, "Precondition failed.");
            }

            var persisted = Clone(entity);
            persisted.ETag = NextEtag();
            _store[(persisted.PartitionKey, persisted.RowKey)] = persisted;
            return Task.CompletedTask;
        }

        private ETag NextEtag() => new($"etag-{_etagCounter++}");

        private static TableEntity Clone(TableEntity entity)
        {
            var clone = new TableEntity(entity.PartitionKey, entity.RowKey)
            {
                ETag = entity.ETag,
                Timestamp = entity.Timestamp
            };

            foreach (var pair in entity)
            {
                clone[pair.Key] = pair.Value;
            }

            return clone;
        }
    }
}
