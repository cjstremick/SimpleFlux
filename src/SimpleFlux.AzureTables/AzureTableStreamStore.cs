using Azure;
using Azure.Data.Tables;

namespace SimpleFlux.AzureTables;

/// <summary>
/// An <see cref="IStreamStore"/> implementation backed by Azure Table Storage.
/// </summary>
/// <remarks>
/// All streams live in a single Azure Table. Each stream's events share a partition key
/// (the stream id) and a header row (<see cref="FluxHeader"/>) tracks the current
/// version. Appends write the events and the header update as one table transaction;
/// optimistic concurrency is enforced with the header's <see cref="ETag"/>.
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
    public async Task AppendToStreamAsync(string streamId, int expectedVersion, IReadOnlyList<FluxEventRecord> events, int newVersion, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0) return;

        var header = await ReadHeaderAsync(streamId, cancellationToken);
        ValidateExpectedVersion(streamId, expectedVersion, header);

        var actions = new List<TableTransactionAction>(events.Count + 1);
        foreach (var record in events)
        {
            actions.Add(new TableTransactionAction(TableTransactionActionType.Add, ToEntity(streamId, record)));
        }

        var headerVersion = header?.Version ?? 0;
        var headerEntity = header ?? new FluxHeader(streamId);
        if (headerVersion >= newVersion)
            throw new ArgumentException(
                $"newVersion ({newVersion}) must be greater than the current stream version ({headerVersion}).");

        // Concurrency enforcement at the storage level:
        //  - new stream  -> Add (insert) fails with 409 if the stream appeared meanwhile
        //  - existing    -> UpdateReplace with the read ETag fails with 412 if it changed
        //  - any version -> unconditional UpdateReplace
        var headerActionType = expectedVersion switch
        {
            -1 => TableTransactionActionType.Add,
            -2 => TableTransactionActionType.UpdateReplace,
            _ => TableTransactionActionType.UpdateReplace
        };
        var headerETag = expectedVersion switch
        {
            -1 => ETag.All,
            -2 => ETag.All,
            _ => header?.ETag ?? ETag.All
        };

        headerEntity.Version = newVersion;
        actions.Add(new TableTransactionAction(headerActionType, headerEntity, headerETag));

        try
        {
            await _tableClient.SubmitTransactionAsync(actions, cancellationToken);
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
            .OrderBy(e => (int) e["Version"])
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

    private static void ValidateExpectedVersion(string streamId, int expectedVersion, FluxHeader? header)
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
            Version = (int) entity["Version"],
            Properties = properties
        };
    }
}