# SimpleFlux

SimpleFlux is a simple event sourcing library for .NET. Append **events** to **streams**
and rebuild **projections** by replaying them — with **pluggable storage backends**
(Azure Table Storage, flat files, and in-memory). Inspired by the excellent Streamstone project.

| CI | NuGet | License |
|---|---|---|
| [![CI](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml) | [![NuGet](https://img.shields.io/nuget/v/SimpleFlux)](https://www.nuget.org/packages/SimpleFlux) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) |

## Why SimpleFlux?

Event sourcing shouldn't require a massive framework. SimpleFlux gives you:

- **3 lines to get started** — define an event, pick a backend, append
- **Swap storage without changing code** — the same `FluxStore` works against InMemory, FlatFile, or Azure Tables
- **Optimistic concurrency for free** — concurrent appends are safe; conflicts throw `FluxConcurrencyException`
- **Tiny contract** — backends implement 3 methods (`Append`, `Read`, `GetMetadata`); everything else is handled by the core
- **Zero external dependencies** for the core library

## Quickstart

```bash
dotnet add package SimpleFlux
dotnet add package SimpleFlux.InMemory   # or SimpleFlux.FlatFile, SimpleFlux.AzureTables
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleFlux;
using SimpleFlux.InMemory;

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
    .WithEvent<ItemAdded>()                    // register event types
    .UseInMemory();

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
Console.WriteLine($"Stock: {inventory.Quantity}");  // Stock: 10
```

No DI? Use the backends directly:

```csharp
var store = new FluxStore(new InMemoryStreamStore(),
    new FluxOptions { EventTypes = { typeof(ItemAdded) } });
// or
var store = new FluxStore(new FlatFileStreamStore("/path/to/streams"));
```

## Storage Backends

| Package | Backend | Use for |
|---|---|---|
| **SimpleFlux.InMemory** | In-memory dictionaries | Quickstarts, tests, demos — zero setup |
| **SimpleFlux.FlatFile** | JSONL files on disk | Local persistence, development, offline use |
| **SimpleFlux.AzureTables** | Azure Table Storage | Production (needs Azurite or Azure account) |

### InMemory

```csharp
services.AddSimpleFlux()
    .WithEvents<ItemAdded, ItemRemoved>()
    .UseInMemory();
```

Zero dependencies. Data lost on process exit. Fastest option for tests.

### FlatFile

```csharp
services.AddSimpleFlux()
    .ScanAssemblyOf<ItemAdded>()               // scan assembly for all events
    .UseFlatFile("/path/to/streams");
```

Each stream gets its own directory with an append-only `events.jsonl` and `meta.json`.
Per-stream file locking for concurrency. Zero external dependencies. Durable to disk.

**Storage layout:**
```
/path/to/streams/
  {streamId}/
    events.jsonl   ← append-only JSON lines
    meta.json      ← stream version tracking
```

### Azure Tables

```csharp
using Azure.Data.Tables;

services.AddSimpleFlux()
    .ScanAssemblyOf<ItemAdded>()
    .UseAzureTables(new TableClient("UseDevelopmentStorage=true", "FluxStore"));
```

Needs [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
for local development or a real Azure Storage account for production.
Batches larger than 99 events are automatically chunked across transactions.

## API Overview

| Member | Purpose |
|---|---|
| `FluxStore(IStreamStore, FluxOptions?)` | Main facade; discovers all `FluxEvent` types |
| `FluxStore.AddEvent(e)` | Append one event; stream version advances atomically |
| `FluxStore.AddEvents(events)` | Append many events, one atomic operation per stream |
| `FluxStore.ProjectTo<T>(id)` | Rebuild a projection by replaying a stream (`null` if empty) |
| `FluxStore.StreamExists(id)` / `GetStreamVersion(id)` | Stream metadata queries |
| `IStreamStore` | The storage contract backends implement (see [ARCHITECTURE.md](docs/ARCHITECTURE.md)) |
| `FluxEvent` / `[FluxEvent]` / `[FluxProperty]` | Event contracts (storage-neutral) |
| `FluxProjection` | Base for read models: implement `Apply(SomeEvent)` per event type |

All async methods accept a `CancellationToken`.

## Concurrency

Appends are **optimistic-concurrency safe**: `AddEvent`/`AddEvents` record the stream
version they expect, and the backend rejects the write with
`FluxConcurrencyException` if the stream changed in the meantime. Concurrent appends
to the same stream must be retried or serialized by the caller.

```csharp
try
{
    await store.AddEvent(new ItemAdded("ABC-123", 5));
}
catch (FluxConcurrencyException ex)
{
    // Stream was modified since we last read it — retry with fresh version
    Console.WriteLine($"Conflict: expected v{ex.ExpectedVersion}, actual v{ex.ActualVersion}");
}
```

## Writing a Custom Backend

Implement `IStreamStore` (3 methods) and add a builder extension:

```csharp
public class MyStreamStore : IStreamStore
{
    public Task AppendToStreamAsync(string streamId, long expectedVersion,
        IReadOnlyList<FluxEventRecord> events, long newVersion,
        CancellationToken ct = default) { /* ... */ }

    public Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(
        string streamId, CancellationToken ct = default) { /* ... */ }

    public Task<FluxStreamMetadata?> GetStreamMetadataAsync(
        string streamId, CancellationToken ct = default) { /* ... */ }
}

public static class MyBuilderExtensions
{
    public static IFluxBuilder UseMyBackend(this IFluxBuilder builder, string connectionString)
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IStreamStore),
            _ => new MyStreamStore(connectionString),
            builder.Options.StoreLifetime));
        return builder;
    }
}
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full contract specification.

## Testing

```bash
dotnet test   # runs InMemory + FlatFile contract tests
```

The test suite exercises the `IStreamStore` contract against every backend:
append, read, metadata, concurrency, large batches, and FluxStore integration.
Azure Tables tests require Azurite — skip them with `--filter "FullyQualifiedName!~AzureTables"`.

## Building & Running the Sample

```bash
dotnet restore
dotnet build
dotnet run --project sample/SimpleFlux.Sample   # interactive menu, uses InMemory by default
```

Swap the sample to FlatFile or Azure Tables by uncommenting the corresponding line
in `Program.cs`.

## Building & Running the Benchmarks

The benchmark suite (BenchmarkDotNet) compares all 3 backends across batch sizes.

```bash
export PATH="$HOME/.dotnetsdk:$PATH"
export DOTNET_ROOT="$HOME/.dotnetsdk"

dotnet build -c Release benchmarks/SimpleFlux.Benchmarks
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*Append*'
```

Azure Tables benchmarks need Azurite:
```bash
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

## Packages

| Package | What it is |
|---|---|
| **SimpleFlux** | Core: `IStreamStore`, `FluxStore`, events, projections, DI registration |
| **SimpleFlux.AzureTables** | Azure Table Storage backend |
| **SimpleFlux.InMemory** | Zero-dependency in-memory backend |
| **SimpleFlux.FlatFile** | Flat-file JSONL backend |

## Roadmap

- ✅ Storage backend abstraction (v2)
- ✅ Cancellation tokens
- ✅ Optimistic concurrency
- ✅ .NET 10
- ✅ Release pipeline
- ✅ FlatFile backend
- ✅ Contract test suite
- Backlog: snapshot support, stream deletion, metadata/correlation IDs, additional backends

Ideas welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Architecture notes live in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Releasing

All SimpleFlux packages publish together from one workflow run. See
[RELEASING.md](RELEASING.md) for the prerelease → promote-to-stable guide.

## License

[MIT](LICENSE) © Cj Stremick
