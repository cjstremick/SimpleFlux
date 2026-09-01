using SimpleFlux;
using Xunit;

namespace SimpleFlux.Tests.Contract;

/// <summary>
/// Shared contract tests that every IStreamStore implementation must pass.
/// Each backend gets its own subclass that provides the store via <see cref="CreateStore"/>.
/// </summary>
public abstract class StreamStoreContractTests : IDisposable
{
    private readonly IStreamStore _store;
    private readonly FluxStore _fluxStore;
    private readonly List<string> _createdStreams = new();

    protected StreamStoreContractTests()
    {
        _store = CreateStore();
        _fluxStore = new FluxStore(_store,
            new FluxOptions { EventTypes = { typeof(TestEvent), typeof(TestEventB) } });
    }

    /// <summary>
    /// Creates a fresh IStreamStore instance for the backend under test.
    /// </summary>
    protected abstract IStreamStore CreateStore();

    /// <summary>
    /// Creates a fresh FluxStore for the backend under test (used in FluxStore-level tests).
    /// </summary>
    protected FluxStore CreateFluxStore() =>
        new FluxStore(CreateStore(),
            new FluxOptions { EventTypes = { typeof(TestEvent), typeof(TestEventB) } });

    protected string NewStreamId()
    {
        var id = $"test-{Guid.NewGuid():N}";
        _createdStreams.Add(id);
        return id;
    }

    public void Dispose()
    {
        // Backends handle cleanup via their own lifecycle (InMemory GC, FlatFile temp dirs, etc.)
        GC.SuppressFinalize(this);
    }

    // --- Contract: Append + Read ---

    [Fact]
    public async Task Append_OneEvent_ReadBack_VersionMatches()
    {
        var streamId = NewStreamId();
        var record = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 1,
            Properties = new Dictionary<string, object?> { ["Value"] = 42 }
        };

        await _store.AppendToStreamAsync(streamId, -1, new[] { record }, 1);

        var results = await _store.ReadStreamAsync(streamId);
        Assert.Single(results);
        Assert.Equal(1, results[0].Version);
        Assert.Equal("TestEvent", results[0].EventTypeName);
        Assert.Equal(42, results[0].Properties["Value"]);
    }

    [Fact]
    public async Task Append_MultipleEvents_ReadBack_OrderedByVersion()
    {
        var streamId = NewStreamId();
        var records = Enumerable.Range(1, 5).Select(i => new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = i,
            Properties = new Dictionary<string, object?> { ["Value"] = i * 10 }
        }).ToArray();

        await _store.AppendToStreamAsync(streamId, -1, records, 5);

        var results = await _store.ReadStreamAsync(streamId);
        Assert.Equal(5, results.Count);
        for (var i = 0; i < 5; i++)
            Assert.Equal(i + 1, results[i].Version);
    }

    [Fact]
    public async Task Append_Batch_Large_200Events_AllPresent()
    {
        var streamId = NewStreamId();
        var count = 200;
        var records = Enumerable.Range(1, count).Select(i => new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = i,
            Properties = new Dictionary<string, object?> { ["Value"] = i }
        }).ToArray();

        await _store.AppendToStreamAsync(streamId, -1, records, count);

        var results = await _store.ReadStreamAsync(streamId);
        Assert.Equal(count, results.Count);
        Assert.Equal(1, results[0].Version);
        Assert.Equal(count, results[^1].Version);
    }

    // --- Contract: Metadata ---

    [Fact]
    public async Task GetMetadata_ExistingStream_ReturnsVersion()
    {
        var streamId = NewStreamId();
        var record = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 1,
            Properties = new Dictionary<string, object?> { ["Value"] = 1 }
        };

        await _store.AppendToStreamAsync(streamId, -1, new[] { record }, 1);

        var metadata = await _store.GetStreamMetadataAsync(streamId);
        Assert.NotNull(metadata);
        Assert.Equal(streamId, metadata.StreamId);
        Assert.Equal(1, metadata.Version);
    }

    [Fact]
    public async Task GetMetadata_NonExistentStream_ReturnsNull()
    {
        var metadata = await _store.GetStreamMetadataAsync(NewStreamId());
        Assert.Null(metadata);
    }

    [Fact]
    public async Task ReadStream_NonExistentStream_ReturnsEmpty()
    {
        var results = await _store.ReadStreamAsync(NewStreamId());
        Assert.Empty(results);
    }

    // --- Contract: Concurrency ---

    [Fact]
    public async Task Append_ExpectedVersionMismatch_ThrowsFluxConcurrencyException()
    {
        var streamId = NewStreamId();
        var record = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 1,
            Properties = new Dictionary<string, object?> { ["Value"] = 1 }
        };

        // Create stream at version 1
        await _store.AppendToStreamAsync(streamId, -1, new[] { record }, 1);

        // Try to append expecting version 0 (stream is at 1)
        var conflict = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 2,
            Properties = new Dictionary<string, object?> { ["Value"] = 2 }
        };

        var ex = await Assert.ThrowsAsync<FluxConcurrencyException>(
            () => _store.AppendToStreamAsync(streamId, 0, new[] { conflict }, 2));

        Assert.Equal(streamId, ex.StreamId);
        Assert.Equal(0, ex.ExpectedVersion);
        Assert.Equal(1, ex.ActualVersion);
    }

    [Fact]
    public async Task Append_NoStream_ToExistingStream_Throws()
    {
        var streamId = NewStreamId();
        var record = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 1,
            Properties = new Dictionary<string, object?> { ["Value"] = 1 }
        };

        await _store.AppendToStreamAsync(streamId, -1, new[] { record }, 1);

        var conflict = new FluxEventRecord
        {
            EventTypeName = "TestEvent",
            Version = 2,
            Properties = new Dictionary<string, object?> { ["Value"] = 2 }
        };

        await Assert.ThrowsAsync<FluxConcurrencyException>(
            () => _store.AppendToStreamAsync(streamId, -1, new[] { conflict }, 2));
    }

    [Fact]
    public async Task Append_AnyVersion_AlwaysSucceeds()
    {
        var streamId = NewStreamId();
        for (var i = 1; i <= 5; i++)
        {
            var record = new FluxEventRecord
            {
                EventTypeName = "TestEvent",
                Version = i,
                Properties = new Dictionary<string, object?> { ["Value"] = i }
            };
            await _store.AppendToStreamAsync(streamId, -2, new[] { record }, i);
        }

        var results = await _store.ReadStreamAsync(streamId);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task Append_EmptyBatch_ReadsEmpty()
    {
        var streamId = NewStreamId();
        await _store.AppendToStreamAsync(streamId, -1, Array.Empty<FluxEventRecord>(), 0);

        var results = await _store.ReadStreamAsync(streamId);
        Assert.Empty(results);
    }

    // --- Contract: FluxStore integration ---

    [Fact]
    public async Task FluxStore_AddEvent_AndReadBack()
    {
        var store = CreateFluxStore();
        var streamId = NewStreamId();

        var @event = new TestEvent(streamId, 99);
        await store.AddEvent(@event);

        Assert.Equal(1, @event.Version);

        var version = await store.GetStreamVersion(streamId);
        Assert.Equal(1, version);
    }

    [Fact]
    public async Task FluxStore_AddEvents_AndProject()
    {
        var store = CreateFluxStore();
        var streamId = NewStreamId();

        var events = new FluxEvent[]
        {
            new TestEvent(streamId, 10),
            new TestEvent(streamId, 20),
            new TestEvent(streamId, 30),
        };
        await store.AddEvents(events);

        var projection = await store.ProjectTo<TestProjection>(streamId);
        Assert.NotNull(projection);
        Assert.Equal(60, projection.Total);
    }

    [Fact]
    public async Task FluxStore_StreamExists()
    {
        var store = CreateFluxStore();
        var streamId = NewStreamId();

        Assert.False(await store.StreamExists(streamId));

        await store.AddEvent(new TestEvent(streamId, 1));

        Assert.True(await store.StreamExists(streamId));
    }

    [Fact]
    public async Task FluxStore_EmptyStream_ProjectTo_ReturnsNull()
    {
        var store = CreateFluxStore();
        var result = await store.ProjectTo<TestProjection>(NewStreamId());
        Assert.Null(result);
    }

    // --- Test event types ---

    public class TestEvent : FluxEvent
    {
        public TestEvent(string id) : base(id) { }
        public TestEvent(string id, int value) : base(id) => Value = value;

        [FluxProperty("Value")]
        public int Value { get; set; }
    }

    public class TestEventB : FluxEvent
    {
        public TestEventB(string id) : base(id) { }
        public TestEventB(string id, string label) : base(id) => Label = label;

        [FluxProperty("Label")]
        public string Label { get; set; } = string.Empty;
    }

    public class TestProjection : FluxProjection
    {
        public TestProjection(string id) : base(id) { }
        public int Total { get; private set; }
        public void Apply(TestEvent e) => Total += e.Value;
    }
}
