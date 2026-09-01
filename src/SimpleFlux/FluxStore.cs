namespace SimpleFlux;

/// <summary>
/// The main entry point for reading and writing event streams.
/// </summary>
/// <remarks>
/// <see cref="FluxStore"/> is storage-agnostic: it talks to an <see cref="IStreamStore"/>
/// implementation (Azure Tables, InMemory, ...), owns event discovery and version
/// assignment, and hydrates <see cref="FluxEvent"/> instances from storage records.
/// </remarks>
public class FluxStore
{
    private const long NoStream = -1L;

    private readonly IStreamStore _streamStore;
    private readonly List<KnownFluxEventType> _knownEventTypes;

    /// <summary>
    /// Creates a store over the given stream store.
    /// </summary>
    /// <param name="streamStore">The backend storage implementation.</param>
    /// <param name="options">Optional configuration (event discovery assemblies).</param>
    public FluxStore(IStreamStore streamStore, FluxOptions? options = null)
    {
        _streamStore = streamStore;
        _knownEventTypes = DiscoverEventTypes(options ?? new FluxOptions());
    }

    /// <summary>
    /// Appends a single event to its stream and advances the stream version.
    /// </summary>
    /// <param name="event">The event to append. Its <see cref="FluxEvent.Id"/> selects the stream.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <exception cref="FluxConcurrencyException">Thrown when the stream was modified concurrently.</exception>
    public async Task AddEvent(FluxEvent @event, CancellationToken cancellationToken = default)
    {
        var expectedVersion = await ResolveExpectedVersionAsync(@event.Id, cancellationToken);
        var newVersion = NextVersion(expectedVersion);
        @event.Version = newVersion;
        var record = ToRecord(@event);
        await _streamStore.AppendToStreamAsync(@event.Id, expectedVersion, new[] { record }, newVersion, cancellationToken);
    }

    /// <summary>
    /// Appends a batch of events, grouped by stream.
    /// </summary>
    /// <remarks>
    /// Events are grouped by <see cref="FluxEvent.Id"/>; each group is appended as one
    /// atomic backend operation and the groups are sent concurrently. A conflicting
    /// append fails the whole batch with <see cref="FluxConcurrencyException"/>.
    /// </remarks>
    /// <param name="events">The events to append. Events with the same id share a stream.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <exception cref="FluxConcurrencyException">Thrown when a stream was modified concurrently.</exception>
    public async Task AddEvents(IEnumerable<FluxEvent> events, CancellationToken cancellationToken = default)
    {
        var fluxEvents = events as FluxEvent[] ?? events.ToArray();
        if (fluxEvents.Length == 0) return;

        var tasks = fluxEvents
            .GroupBy(e => e.Id)
            .Select(group => AppendGroupAsync(group, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task AppendGroupAsync(IEnumerable<FluxEvent> group, CancellationToken cancellationToken)
    {
        var events = group as FluxEvent[] ?? group.ToArray();
        var streamId = events[0].Id;
        var expectedVersion = await ResolveExpectedVersionAsync(streamId, cancellationToken);

        var version = NextVersion(expectedVersion);
        var records = new List<FluxEventRecord>(events.Length);
        foreach (var @event in events)
        {
            @event.Version = version;
            records.Add(ToRecord(@event));
            version++;
        }

        await _streamStore.AppendToStreamAsync(streamId, expectedVersion, records, version - 1, cancellationToken);
    }

    private async Task<long> ResolveExpectedVersionAsync(string streamId, CancellationToken cancellationToken)
    {
        var metadata = await _streamStore.GetStreamMetadataAsync(streamId, cancellationToken);
        return metadata?.Version ?? NoStream;
    }

    private static long NextVersion(long expectedVersion) => expectedVersion == NoStream ? 1L : expectedVersion + 1;

    private static List<KnownFluxEventType> DiscoverEventTypes(FluxOptions options)
    {
        var explicitTypes = options.EventTypes
            .Where(t => t.IsSubclassOf(typeof(FluxEvent)))
            .ToList();
        var assemblies = options.EventAssemblies.ToList();

        if (explicitTypes.Count == 0 && assemblies.Count == 0)
        {
            throw new InvalidOperationException(
                "No event types registered. Use WithEvent<T>(), WithEvents<T1,T2>(), " +
                "or ScanAssemblyOf<TMarker>() to register event types before creating the store.");
        }

        var discovered = new List<KnownFluxEventType>(explicitTypes.Count + assemblies.Count * 4);
        discovered.AddRange(explicitTypes.Select(ToKnownEventType));
        foreach (var assembly in assemblies)
        {
            discovered.AddRange(assembly
                .GetTypes()
                .Where(t => t.IsSubclassOf(typeof(FluxEvent)))
                .Select(ToKnownEventType));
        }

        // A type registered explicitly AND found in a scanned assembly must not be duplicated.
        return discovered
            .GroupBy(k => k.Type)
            .Select(g => g.First())
            .ToList();
    }

    private static KnownFluxEventType ToKnownEventType(Type type)
    {
        var fluxEventAttribute = type
            .GetCustomAttributes(true)
            .OfType<FluxEventAttribute>()
            .SingleOrDefault();
        return new KnownFluxEventType
        {
            Name = fluxEventAttribute?.Name ?? type.Name,
            Type = type
        };
    }

    private FluxEventRecord ToRecord(FluxEvent @event)
    {
        var eventType = @event.GetType();
        var knownEventType = _knownEventTypes.SingleOrDefault(t => t.Type == eventType);
        if (knownEventType == null) throw new Exception($"Event type {eventType} not found");

        var properties = new Dictionary<string, object?>();
        foreach (var property in eventType.GetProperties())
        {
            var attribute = property
                .GetCustomAttributes(true)
                .OfType<FluxPropertyAttribute>()
                .SingleOrDefault();
            if (attribute != null)
            {
                properties[attribute.Name] = property.GetValue(@event);
            }
        }

        return new FluxEventRecord
        {
            EventTypeName = knownEventType.Name,
            Version = @event.Version,
            Properties = properties
        };
    }

    private FluxEvent Hydrate(string streamId, FluxEventRecord record)
    {
        var knownEventType = _knownEventTypes.SingleOrDefault(t => t.Name == record.EventTypeName);
        if (knownEventType == null) throw new Exception($"Event type {record.EventTypeName} not found");
        if (Activator.CreateInstance(knownEventType.Type, streamId) is not FluxEvent @event)
            throw new Exception($"Could not create instance of {knownEventType.Type}");

        @event.Version = record.Version;
        foreach (var property in knownEventType.Type.GetProperties())
        {
            var attribute = property
                .GetCustomAttributes(true)
                .OfType<FluxPropertyAttribute>()
                .SingleOrDefault();
            if (attribute != null && record.Properties.TryGetValue(attribute.Name, out var value))
            {
                property.SetValue(@event, value);
            }
        }

        return @event;
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

        var records = await _streamStore.ReadStreamAsync(id, cancellationToken);
        var events = records.Select(r => Hydrate(id, r)).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (events.Length == 0) return null;

        projection.Load(events);
        return projection;
    }

    /// <summary>
    /// Checks whether a stream exists (i.e. has at least one event).
    /// </summary>
    /// <param name="id">The stream id.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns><c>true</c> when the stream has at least one event.</returns>
    public async Task<bool> StreamExists(string id, CancellationToken cancellationToken = default)
    {
        var metadata = await _streamStore.GetStreamMetadataAsync(id, cancellationToken);
        return metadata != null;
    }

    /// <summary>
    /// Gets the current version of a stream (the version of the last appended event).
    /// </summary>
    /// <param name="id">The stream id.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The stream version, or <c>null</c> when the stream does not exist.</returns>
    public async Task<long?> GetStreamVersion(string id, CancellationToken cancellationToken = default)
    {
        var metadata = await _streamStore.GetStreamMetadataAsync(id, cancellationToken);
        return metadata?.Version;
    }

    private class KnownFluxEventType
    {
        public string Name { get; set; } = null!;
        public Type Type { get; set; } = null!;
    }
}