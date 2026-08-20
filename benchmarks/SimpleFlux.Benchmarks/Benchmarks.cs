using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Azure.Data.Tables;
using SimpleFlux;
using SimpleFlux.AzureTables;
using SimpleFlux.InMemory;

namespace SimpleFlux.Benchmarks;

/// AzureTables needs Azurite running on the local devstore endpoints
/// (blob 10000, queue 10001, table 10002) — e.g.
/// `docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite`.
///
/// NOTE: entry point lives in Program.cs (BenchmarkDotNet AutoStart). Build with the
/// .NET 10 SDK installed.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks
{
    // The 100-entity Azure Table Storage transaction limit is the known cliff.
    private const int AzureBatchLimit = 100;

    [Params("InMemory", "AzureTables")]
    public string Backend { get; set; } = "InMemory";

    // Batch-size sweep to surface the 100-entity transaction cliff on AzureTables.
    // Note: BatchSize only drives P2 (Append_Batch).
    [Params(1, 10, 25, 50, 75, 100, 125)]
    public int BatchSize { get; set; }

    private FluxStore? _store;
    private string? _streamId;
    private string? _projectStreamId;

    [GlobalSetup]
    public async Task Setup()
    {
        _store = Backend switch
        {
            "InMemory" => new FluxStore(new InMemoryStreamStore(),
                new FluxOptions { EventTypes = { typeof(BenchItemAdded), typeof(BenchItemRemoved) } }),
            "AzureTables" => await CreateAzureTableStoreAsync(),
            _ => throw new NotSupportedException($"Unknown backend: {Backend}")
        };
        // Unique-per-parameter-combo stream ids so iterations don't collide.
        _streamId = $"b-{Backend}-{Guid.NewGuid():N}";
        _projectStreamId = $"b-proj-{Backend}-{Guid.NewGuid():N}";
    }

    private static async Task<FluxStore> CreateAzureTableStoreAsync()
    {
        // devstore → blob:10000, queue:10001, table:10002 with the well-known key.
        var client = new TableClient("UseDevelopmentStorage=true", "FluxBenchmarks");
        await client.CreateIfNotExistsAsync();
        return new FluxStore(new AzureTableStreamStore(client),
            new FluxOptions { EventTypes = { typeof(BenchItemAdded), typeof(BenchItemRemoved) } });
    }

    private FluxStore Store => _store ?? throw new InvalidOperationException("Not initialized.");

    // --- P1: single-event append latency ---
    [Benchmark(Baseline = true)]
    public async Task Append_SingleEvent()
    {
        await Store.AddEvent(new BenchItemAdded($"{_streamId}-single"));
    }

    // --- P2: batch append throughput vs batch size ---
    [Benchmark]
    public async Task Append_Batch()
    {
        var events = Enumerable.Range(0, BatchSize)
            .Select(_ => new BenchItemAdded(_streamId!));
        await Store.AddEvents(events);
    }

    // --- P3: read a 180-event stream ---
    // Warm once (idempotent: appends only if the stream is empty), then time the projection/read.
    [Benchmark]
    public async Task Read_Stream()
    {
        await WarmTo180Async(_projectStreamId!);
        await Store.ProjectTo<BenchInventoryProjection>(_projectStreamId!);
    }

    // --- P4: projection over a 180-event stream (the LargeStreamModule baseline) ---
    [Benchmark]
    public async Task Project_Stream()
    {
        await WarmTo180Async(_projectStreamId!);
        await Store.ProjectTo<BenchInventoryProjection>(_projectStreamId!);
    }

    // --- P5: concurrency — N concurrent writers to one stream, measure collision rate ---
    [Benchmark]
    public async Task<int> Concurrency_Contention()
    {
        const int writers = 8;
        var streamId = $"b-conc-{Guid.NewGuid():N}";
        // Prime with one event so there is a version to conflict on.
        await Store.AddEvent(new BenchItemAdded(streamId));

        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            try
            {
                // Each writer appends expecting the primed version; only one can win.
                await Store.AddEvent(new BenchItemAdded(streamId));
                return 0; // success
            }
            catch (FluxConcurrencyException)
            {
                return 1; // conflict
            }
        }));

        var results = await Task.WhenAll(tasks);
        GC.KeepAlive(AzureBatchLimit); // keep the const referenced (documents the cliff)
        return results.Sum();
    }

    // --- P6: reflection overhead — event discovery (per Store construction) + mapping ---
    [Benchmark]
    public int Reflection_Overhead()
    {
        // Discovery happens at FluxStore construction; timing one construction captures the scan cost.
        _ = new FluxStore(new InMemoryStreamStore(),
            new FluxOptions { EventTypes = { typeof(BenchItemAdded) } });
        return 1;
    }

    private async Task WarmTo180Async(string streamId)
    {
        // Idempotent warm: only append if the stream doesn't already have 180 events.
        var version = await Store.GetStreamVersion(streamId);
        if (version.GetValueOrDefault(-1) >= 180) return;

        // NOTE: AppendToStreamAsync submits one Azure Table transaction (events + header).
        // Azure Tables limits a transaction to 100 entities, and the 2.0.0 store does NOT
        // chunk (known issue, see AGENTS.md "Unchunked batch limit"). So we warm with
        // explicit chunks of <=99 events to stay under the cliff; this both lets the
        // read/project benchmarks run against AzureTables and demonstrates the workaround.
        var remaining = 180 - Math.Max(0, version.GetValueOrDefault(-1));
        const int chunkSize = 50;
        while (remaining > 0)
        {
            var n = Math.Min(chunkSize, remaining);
            var warm = Enumerable.Range(0, n).Select(_ => new BenchItemAdded(streamId));
            await Store.AddEvents(warm);     // one chunk
            remaining -= n;
        }
    }

    // -- event / projection fixtures (mirror of sample events) --
    [FluxEvent("BenchItemAdded")]
    public class BenchItemAdded : FluxEvent
    {
        public BenchItemAdded(string id) : base(id) { }
        public BenchItemAdded(string id, int quantity) : base(id) => Quantity = quantity;
        [FluxProperty("Quantity")] public int Quantity { get; set; }
    }

    [FluxEvent("BenchItemRemoved")]
    public class BenchItemRemoved : FluxEvent
    {
        public BenchItemRemoved(string id) : base(id) { }
        public BenchItemRemoved(string id, int quantity) : base(id) => Quantity = quantity;
        [FluxProperty("Quantity")] public int Quantity { get; set; }
    }

    public class BenchInventoryProjection : FluxProjection
    {
        public BenchInventoryProjection(string id) : base(id) { }
        public int Quantity { get; private set; }
        public void Apply(BenchItemAdded e) => Quantity += e.Quantity;
        public void Apply(BenchItemRemoved e) => Quantity = Math.Max(0, Quantity - e.Quantity);
    }
}
