# SimpleFlux

Simple Flux is a simple event sourcing library for .NET. Append **events** to **streams**
and rebuild **projections** by replaying them — with **pluggable storage backends**
(Azure Table Storage, in-memory, and more to come). Inspired by the excellent Streamstone project.

| CI | NuGet | License |
|---|---|---|
| [![CI](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml) | [![NuGet](https://img.shields.io/nuget/v/SimpleFlux)](https://www.nuget.org/packages/SimpleFlux) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) |

## Packages

| Package | What it is |
|---|---|
| **SimpleFlux** | Core: abstractions (`IStreamStore`), `FluxStore` facade, events, projections, DI registration (`AddSimpleFlux()`) |
| **SimpleFlux.AzureTables** | Azure Table Storage backend (`UseAzureTables(tableClient)`) |
| **SimpleFlux.InMemory** | Zero-dependency in-memory backend (`UseInMemory()`) — quickstarts, demos, tests |

## Quickstart

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleFlux;
using SimpleFlux.InMemory;   // or SimpleFlux.AzureTables

// 1. Define an event
[FluxEvent("ItemAdded")]
public class ItemAdded : FluxEvent
{
    public ItemAdded(string sku, int quantity) : base(sku) => Quantity = quantity;

    [FluxProperty("Quantity")]
    public int Quantity { get; set; }
}

// 2. Register SimpleFlux with a backend
var services = new ServiceCollection();
services
    .AddSimpleFlux()
    .WithAssemblyEvents<ItemAdded>()   // events from this assembly
    .UseInMemory();
//   .UseAzureTables(new TableClient("UseDevelopmentStorage=true", "FluxStore"));

var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<FluxStore>();

// 3. Append events to a stream (stream = the event's Id)
await store.AddEvent(new ItemAdded("ABC-123", 10));

// 4. Rebuild a projection from the stream
public class ItemInventory : FluxProjection
{
    public ItemInventory(string id) : base(id) { }
    public int Quantity { get; private set; }
    public void Apply(ItemAdded e) => Quantity += e.Quantity;
}

var inventory = await store.ProjectTo<ItemInventory>("ABC-123");
```

No DI? Use the backends directly: `new FluxStore(new InMemoryStreamStore())` or
`new FluxStore(new AzureTableStreamStore(tableClient))`.

## API overview

| Member | Purpose |
|---|---|
| `FluxStore(IStreamStore, FluxOptions?)` | Main facade; discovers all `FluxEvent` types in the given assemblies (all loaded by default) |
| `FluxStore.AddEvent(e)` | Append one event; stream version advances atomically |
| `FluxStore.AddEvents(events)` | Append many events, one atomic operation per stream |
| `FluxStore.ProjectTo<T>(id)` | Rebuild a projection by replaying a stream (`null` if empty) |
| `FluxStore.StreamExists(id)` / `GetStreamVersion(id)` | Stream metadata queries |
| `IStreamStore` | The storage contract backends implement (see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)) |
| `FluxEvent` / `[FluxEvent]` / `[FluxProperty]` | Event contracts (storage-neutral) |
| `FluxProjection` | Base for read models: implement `Apply(SomeEvent)` per event type |

All async methods accept a `CancellationToken`.

## Concurrency

Appends are **optimistic-concurrency safe**: `AddEvent`/`AddEvents` record the stream
version they expect, and the backend rejects the write with
`FluxConcurrencyException` if the stream changed in the meantime. Concurrent appends
to the same stream must be retried or serialized by the caller.

## Building & running the sample

```bash
dotnet restore
dotnet build
dotnet run --project sample/SimpleFlux.Sample   # interactive menu, uses the in-memory backend
```

Swap the sample to Azure Tables by uncommenting the `UseAzureTables` line in
`Program.cs` (needs [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) or a real storage account).

## Building & running the benchmarks

The benchmark suite lives in `benchmarks/SimpleFlux.Benchmarks` (BenchmarkDotNet,
branch `bench/benchmark-suite`). It needs the **.NET 10 SDK** — on this dev Mac the
SDK is at `~/.dotnetsdk`, so put it on `PATH` first:

```bash
export PATH="$HOME/.dotnetsdk:$PATH"
export DOTNET_ROOT="$HOME/.dotnetsdk"
cd ~/Projects/SimpleFlux
```

**Important:** arguments you want the **benchmark** to see must come **after `--`**
(anything before it is consumed by `dotnet run`). `-j short` = ShortRun (a few
seconds — the fast smoke run); omit it for the longer default job.

```bash
# Build (SDK must be on PATH; do this once before running). NOTE: `dotnet build`
# takes the project as a positional arg (the `--project` form is for `dotnet run`).
dotnet build -c Release benchmarks/SimpleFlux.Benchmarks

# Full suite (perf + safety) with the fast ShortRun job — no Azure needed for InMemory
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*'

# Just the performance scenarios (Append/Read/Project/Concurrency/Reflection)
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*Benchmarks*'

# Just the safety/behavioral assertions
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*SafetyBenchmarks*'
```

The suite is parameterized over the `InMemory` and `AzureTables` backends. The
`InMemory` runs need no setup; the `AzureTables` runs need [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
up on the standard devstore ports (blob 10000, queue 10001, table 10002):

```bash
docker run -d --name azurite-bench -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

Results and JSON artifacts land under `benchmarks/SimpleFlux.Benchmarks/artifacts/`.
See [docs/BENCHMARKING.md](docs/BENCHMARKING.md) for the scenario design and
[docs/BENCHMARKING_RESULTS.md](docs/BENCHMARKING_RESULTS.md) for the last captured run.

## Roadmap

- Done: storage backend abstraction (v2), cancellation tokens, optimistic concurrency, .NET 10, release pipeline
- In progress: test project (parameterized over InMemory + AzureTables backends)
- Backlog (issues): snapshot support, stream deletion, metadata/correlation ids,
  additional backends (EF Core / Postgres), sample typo cleanup

Ideas welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Architecture notes live in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Releasing

All SimpleFlux packages publish together from one workflow run. See
[RELEASING.md](RELEASING.md) for the prerelease → promote-to-stable guide.

## License

[MIT](LICENSE) © Cj Stremick