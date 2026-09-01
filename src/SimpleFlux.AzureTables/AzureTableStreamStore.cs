using Azure;
using Azure.Data.Tables;

namespace SimpleFlux.AzureTables;

/// <summary>
/// An <see cref="IStreamStore"/> implementation backed by Azure Table Storage.
/// </summary>
/// <remarks>
/// All streams live in a single Azure Table. Each stream's events share a partition key
/// (the stream id) and a header row (<see cref="FluxHeader"/>) tracks the current
/// version. Appends write events and header updates as table transactions of at most
/// 100 entities (chunking larger batches); optimistic concurrency is enforced with the
/// header's <see cref="ETag"/>.
/// </remarks>
public sealed class AzureTableStreamStore : IStreamStore
{
    private readonly TableClient _tableClient;

    /// <summary>
    /// Creates a store over the given table client.
    /// </summary>
    /// <param name="tableClient">The Azure Table client used for all storage operations.</param>
    public AzureTableStreamStore(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    /// <inheritdoc />
    public async Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken cancellationToken = default)
    {
        try
        {
            var header = await _tableClient.GetEntityAsync<FluxHeader>(
                streamId,
                FluxHeader.FluxHeaderKey,
                cancellationToken: cancellationToken);
            return new FluxStreamMetadata { StreamId = streamId, Version = header.Value.Version };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task AppendToStreamAsync(string streamId, long expectedVersion, IReadOnlyList<FluxEventRecord> events, long newVersion, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0) return;

        var header = await ReadHeaderAsync(streamId, cancellationToken);
        ValidateExpectedVersion(streamId, expectedVersion, header);

        var headerVersion = header?.Version ?? 0;
        var headerEntity = header ?? new FluxHeader(streamId);
        if (headerVersion >= newVersion)
            throw new ArgumentException(
                $"newVersion ({newVersion}) must be greater than the current stream version ({headerVersion}).");

        // Azure Tables limits a transaction to 100 entities. Each chunk writes at most
        // 99 event rows plus the header row, staying under the limit. The first chunk
        // enforces the expected-version semantics; later chunks append unconditionally,
        // so a batch larger than 99 events is not fully atomic (partial writes on a
        // mid-batch failure are a documented limitation for batches > 99).
        const int maxEventRowsPerTransaction = 99;

        try
        {
            for (var offset = 0; offset < events.Count; offset += maxEventRowsPerTransaction)
            {
                var chunkSize = Math.Min(maxEventRowsPerTransaction, events.Count - offset);
                var actions = new List<TableTransactionAction>(chunkSize + 1);
                for (var i = offset; i < offset + chunkSize; i++)
                {
                    actions.Add(new TableTransactionAction(
                        TableTransactionActionType.Add,
                        ToEntity(streamId, events[i])));
                }

                var isFirstChunk = offset == 0;
                var headerActionType = isFirstChunk && expectedVersion == -1
                    ? TableTransactionActionType.Add
                    : TableTransactionActionType.UpdateReplace;
                var headerETag = !isFirstChunk || expectedVersion is -1 or -2
                    ? ETag.All
                    : header?.ETag ?? ETag.All;

                headerEntity.Version = events[offset + chunkSize - 1].Version;
                actions.Add(new TableTransactionAction(headerActionType, headerEntity, headerETag));

                await _tableClient.SubmitTransactionAsync(actions, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            var actual = await ReadHeaderAsync(streamId, cancellationToken);
            throw new FluxConcurrencyException(streamId, expectedVersion, actual?.Version ?? -1);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken cancellationToken = default)
    {
        var results = new List<TableEntity>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            e => e.PartitionKey == streamId && e.RowKey != FluxHeader.FluxHeaderKey,
            cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }

        return results
            .OrderBy(e => (long) e["Version"])
            .Select(ToRecord)
            .ToArray();
    }

    private async Task<FluxHeader?> ReadHeaderAsync(string streamId, CancellationToken cancellationToken)
    {
        try
        {
            var header = await _tableClient.GetEntityAsync<FluxHeader>(
                streamId,
                FluxHeader.FluxHeaderKey,
                cancellationToken: cancellationToken);
            return header.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static void ValidateExpectedVersion(string streamId, long expectedVersion, FluxHeader? header)
    {
        var actualVersion = header?.Version ?? -1;
        var conflict = expectedVersion switch
        {
            -1 when header != null => true,
            -2 => false,
            _ when actualVersion != expectedVersion => true,
            _ => false
        };
        if (conflict)
        {
            throw new FluxConcurrencyException(streamId, expectedVersion, actualVersion);
        }
    }

    private static TableEntity ToEntity(string streamId, FluxEventRecord record)
    {
        var entity = new TableEntity(streamId, $"F-{Guid.NewGuid()}")
        {
            ["EventType"] = record.EventTypeName,
            ["Version"] = record.Version
        };
        foreach (var (key, value) in record.Properties)
        {
            // Azure Tables has no null column; absent columns stay default on read
            // (which is the same value for null-able properties).
            if (value != null) entity[key] = value;
        }

        return entity;
    }

    private static FluxEventRecord ToRecord(TableEntity entity)
    {
        var properties = new Dictionary<string, object?>();
        foreach (var (key, value) in entity)
        {
            if (key is "PartitionKey" or "RowKey" or "Timestamp" or "ETag" or "EventType" or "Version")
            {
                continue;
            }

            properties[key] = value;
        }

        return new FluxEventRecord
        {
            EventTypeName = (string) entity["EventType"],
            Version = (long) entity["Version"],
            Properties = properties
        };
    }
}