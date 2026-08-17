using Azure.Data.Tables;

namespace SimpleFlux;

/// <summary>
/// The main entry point for reading and writing event streams in Azure Table Storage.
/// </summary>
/// <remarks>
/// All streams live in a single Azure Table. Each stream's events share a partition key
/// (the stream id); a header row (<see cref="FluxHeader"/>) tracks the current version.
/// Event writes and the header update are submitted as one table transaction, so a
/// stream's version never advances without its events being stored.
/// </remarks>
public class FluxStore
{
    private readonly List<KnownFluxEventType> _knownEventTypes;
    private readonly TableClient _tableClient;

    /// <summary>
    /// Creates a store over the given table client.
    /// </summary>
    /// <param name="tableClient">The Azure Table client used for all storage operations.</param>
    public FluxStore(TableClient tableClient)
    {
        _tableClient = tableClient;
        _knownEventTypes = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(FluxEvent)))
            .Select(t =>
            {
                var fluxEventAttribute = t
                    .GetCustomAttributes(true)
                    .OfType<FluxEventAttribute>()
                    .SingleOrDefault();
                return new KnownFluxEventType
                {
                    Name = fluxEventAttribute?.Name ?? t.Name,
                    Type = t
                };
            })
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Appends a single event to its stream and advances the stream version.
    /// </summary>
    /// <param name="event">The event to append. Its <see cref="FluxEvent.Id"/> selects the stream.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public async Task AddEvent(FluxEvent @event, CancellationToken cancellationToken = default)
    {
        var header = await GetHeaderAsync(@event.Id, cancellationToken) ?? new FluxHeader(@event.Id);
        @event.Version = ++header.Version;
        var tableEntity = FromFluxEvent(@event);
        TableTransactionAction[] tableTransactionActions =
        {
            new(TableTransactionActionType.Add, tableEntity),
            new(TableTransactionActionType.UpdateReplace, header)
        };
        await _tableClient.SubmitTransactionAsync(tableTransactionActions, cancellationToken);
    }

    /// <summary>
    /// Appends a batch of events, grouped by stream.
    /// </summary>
    /// <remarks>
    /// Events are grouped by <see cref="FluxEvent.Id"/>; each group is written as a
    /// single table transaction (events plus the header update), and groups are sent
    /// concurrently. Groups larger than Azure's 100-entity transaction limit are not
    /// currently chunked.
    /// </remarks>
    /// <param name="events">The events to append. Events with the same id share a stream.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    public async Task AddEvents(IEnumerable<FluxEvent> events, CancellationToken cancellationToken = default)
    {
        var fluxEvents = events as FluxEvent[] ?? events.ToArray();
        if (!fluxEvents.Any()) return;
        var eventGroups = fluxEvents.GroupBy(e => e.Id);

        var tasks = new List<Task>();
        foreach (var eventGroup in eventGroups)
        {
            var header = await GetHeaderAsync(eventGroup.Key, cancellationToken) ?? new FluxHeader(eventGroup.Key);
            var tableEntities = eventGroup
                .Select(FromFluxEvent)
                .Select(te =>
                {
                    te["Version"] = ++header.Version;
                    return new TableTransactionAction(TableTransactionActionType.Add, te);
                })
                .ToArray();
            var tableTransactionActions = new List<TableTransactionAction>(tableEntities)
            {
                new(TableTransactionActionType.UpdateReplace, header)
            };
            tasks.Add(_tableClient.SubmitTransactionAsync(tableTransactionActions, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }


    private async Task<IReadOnlyList<FluxEvent>> GetEventsAsync(string id, CancellationToken cancellationToken)
    {
        var results = new List<TableEntity>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            e => e.PartitionKey == id && e.RowKey != FluxHeader.FluxHeaderKey,
            cancellationToken: cancellationToken))
        {
            results.Add(entity);
        }

        return results
            .OrderBy(e => e.Timestamp)
            .Select(ToFluxEvent)
            .ToList();
    }

    private async Task<FluxHeader?> GetHeaderAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var header = await _tableClient.GetEntityAsync<FluxHeader>(
                id,
                FluxHeader.FluxHeaderKey,
                cancellationToken: cancellationToken);
            return header.Value;
        }
        catch (OperationCanceledException)
        {
            // Cancellation must propagate: a cancelled caller is not "stream doesn't exist".
            // TaskCanceledException derives from OperationCanceledException, so it is covered here.
            throw;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // 404 means the header row is absent — the stream does not exist.
            return null;
        }
        catch (Exception)
        {
            // Known debt: swallow all other errors (auth, network, etc.) as "stream doesn't exist".
            return null;
        }
    }

    private FluxEvent ToFluxEvent(TableEntity tableEntity)
    {
        var knownEventType = _knownEventTypes.SingleOrDefault(t => t.Name == (string) tableEntity["EventType"]);
        if (knownEventType == null) throw new Exception($"Event type {tableEntity["EventType"]} not found");
        var @event = Activator.CreateInstance(knownEventType.Type, tableEntity["PartitionKey"]);
        if (@event == null) throw new Exception($"Could not create instance of {knownEventType.Type}");
        var properties = @event.GetType().GetProperties();
        foreach (var property in properties)
        {
            var attribute = property
                .GetCustomAttributes(true)
                .OfType<FluxPropertyAttribute>()
                .SingleOrDefault();
            if (attribute != null)
            {
                var value = tableEntity[attribute.Name];
                property.SetValue(@event, value);
            }
        }

        return (FluxEvent) @event;
    }

    private TableEntity FromFluxEvent(FluxEvent @event)
    {
        var eventType = @event.GetType();
        var knownEventType = _knownEventTypes.SingleOrDefault(t => t.Type == eventType);
        if (knownEventType == null) throw new Exception($"Event type {eventType} not found");
        var tableEntity = new TableEntity(@event.Id, $"F-{Guid.NewGuid()}")
        {
            {"EventType", knownEventType.Name},
            {"Version", @event.Version}
        };
        var properties = @event.GetType().GetProperties();
        foreach (var property in properties)
        {
            var attribute = property
                .GetCustomAttributes(true)
                .OfType<FluxPropertyAttribute>()
                .SingleOrDefault();
            if (attribute != null)
            {
                var value = property.GetValue(@event);
                tableEntity.Add(attribute.Name, value);
            }
        }

        return tableEntity;
    }

    /// <summary>
    /// Rebuilds a projection by replaying a stream's events.
    /// </summary>
    /// <typeparam name="T">The projection type; must have a constructor taking the stream id.</typeparam>
    /// <param name="id">The stream id to project.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The rebuilt projection, or <c>null</c> when the stream has no events.</returns>
    public async Task<T?> ProjectTo<T>(string id, CancellationToken cancellationToken = default) where T : FluxProjection
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Activator.CreateInstance(typeof(T), id) is not T projection)
            throw new Exception($"Failed to create Projection {typeof(T)}.");
        var events = await GetEventsAsync(id, cancellationToken);
        var eventsArray = events.ToArray();
        if (eventsArray.Length == 0) return null;
        cancellationToken.ThrowIfCancellationRequested();
        projection.Load(eventsArray);
        return projection;
    }

    /// <summary>
    /// Checks whether a stream exists (i.e. has a header row).
    /// </summary>
    /// <param name="id">The stream id.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns><c>true</c> when the stream has at least one event.</returns>
    public async Task<bool> StreamExists(string id, CancellationToken cancellationToken = default)
    {
        var streamHeader = await GetHeaderAsync(id, cancellationToken);
        return streamHeader != null;
    }

    /// <summary>
    /// Gets the current version of a stream (the version of the last appended event).
    /// </summary>
    /// <param name="id">The stream id.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The stream version, or <c>null</c> when the stream does not exist.</returns>
    public async Task<int?> GetStreamVersion(string id, CancellationToken cancellationToken = default)
    {
        var streamHeader = await GetHeaderAsync(id, cancellationToken);
        return streamHeader?.Version;
    }

    private class KnownFluxEventType
    {
        public string Name { get; set; } = null!;
        public Type Type { get; set; } = null!;
    }
}
