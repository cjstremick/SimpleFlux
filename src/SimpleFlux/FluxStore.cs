using Azure.Data.Tables;

namespace SimpleFlux;

public class FluxStore
{
    private readonly List<KnownFluxEventType> _knownEventTypes;
    private readonly TableClient _tableClient;

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

    public async Task AddEvent(FluxEvent @event)
    {
        var header = GetHeader(@event.Id) ?? new FluxHeader(@event.Id);
        @event.Version = ++header.Version;
        var tableEntity = FromFluxEvent(@event);
        TableTransactionAction[] tableTransactionAcations =
        {
            new(TableTransactionActionType.Add, tableEntity),
            new(TableTransactionActionType.UpdateReplace, header)
        };
        await _tableClient.SubmitTransactionAsync(tableTransactionAcations);
    }

    public async Task AddEvents(IEnumerable<FluxEvent> events)
    {
        var fluxEvents = events as FluxEvent[] ?? events.ToArray();
        if (!fluxEvents.Any()) return;
        var eventGroups = fluxEvents.GroupBy(e => e.Id);

        var tasks = new List<Task>();
        foreach (var eventGroup in eventGroups)
        {
            var header = GetHeader(eventGroup.Key) ?? new FluxHeader(eventGroup.Key);
            var tableEntities = eventGroup
                .Select(FromFluxEvent)
                .Select(te =>
                {
                    te["Version"] = ++header.Version;
                    return new TableTransactionAction(TableTransactionActionType.Add, te);
                })
                .ToArray();
            var tableTransactionAcations = new List<TableTransactionAction>(tableEntities)
            {
                new(TableTransactionActionType.UpdateReplace, header)
            };
            tasks.Add(_tableClient.SubmitTransactionAsync(tableTransactionAcations));
        }

        await Task.WhenAll(tasks);
    }


    public Task<IEnumerable<FluxEvent>> GetEvents(string id)
    {
        var results = _tableClient
            .Query<TableEntity>(e => e.PartitionKey == id && e.RowKey != FluxHeader.FluxHeaderKey)
            .OrderBy(e => e.Timestamp);
        var events = results.Select(ToFluxEvent);
        return Task.FromResult(events);
    }

    private FluxHeader? GetHeader(string id)
    {
        try
        {
            var header = _tableClient.GetEntity<FluxHeader>(id, FluxHeader.FluxHeaderKey);
            return header.Value;
        }
        catch (Exception)
        {
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

    public async Task<T> ProjectTo<T>(string id) where T : FluxProjection
    {
        if (Activator.CreateInstance(typeof(T), id) is not T projection)
            throw new Exception($"Failed to create Projection {typeof(T)}.");
        var events = await GetEvents(id);
        projection.Load(events);
        return projection;
    }

    internal class KnownFluxEventType
    {
        public string Name { get; set; } = null!;
        public Type Type { get; set; } = null!;
    }
}