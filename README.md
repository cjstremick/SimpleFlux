# SimpleFlux

Simple Flux is a simple event sourcing library for .NET. It is based on Azure Table Storage and is inspired by the excellent Streamstone project.

| CI | NuGet | License |
|---|---|---|
| [![CI](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml/badge.svg)](https://github.com/cjstremick/SimpleFlux/actions/workflows/ci.yml) | [![NuGet](https://img.shields.io/nuget/v/SimpleFlux)](https://www.nuget.org/packages/SimpleFlux) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) |

## What it is

SimpleFlux is a small event-sourcing library: you append **events** to **streams**, and
rebuild **projections** (read models) by replaying a stream's events. Everything is
stored in one Azure Table — no extra infrastructure.

```csharp
using Azure.Data.Tables;
using SimpleFlux;

// 1. Define an event
[FluxEvent("ItemAdded")]
public class ItemAdded : FluxEvent
{
    public ItemAdded(string sku, int quantity) : base(sku) => Quantity = quantity;

    [FluxProperty("Quantity")]
    public int Quantity { get; set; }
}

// 2. Append events to a stream (stream = the event's Id)
var tableClient = new TableClient("UseDevelopmentStorage=true", "FluxStore");
var store = new FluxStore(tableClient);
await store.AddEvent(new ItemAdded("ABC-123", 10));

// 3. Rebuild a projection from the stream
public class ItemInventory : FluxProjection
{
    public ItemInventory(string id) : base(id) { }
    public int Quantity { get; private set; }
    public void Apply(ItemAdded e) => Quantity += e.Quantity;
}

var inventory = await store.ProjectTo<ItemInventory>("ABC-123");
```

## API overview

| Member | Purpose |
|---|---|
| `FluxStore(tableClient)` | Entry point; discover all `FluxEvent` subclasses in loaded assemblies |
| `FluxStore.AddEvent(e)` | Append one event, advance the stream version (transactional) |
| `FluxStore.AddEvents(events)` | Append many events, grouped per stream (one transaction per stream) |
| `FluxStore.ProjectTo<T>(id)` | Rebuild a projection by replaying a stream (`null` if empty) |
| `FluxStore.StreamExists(id)` | Whether the stream has any events |
| `FluxStore.GetStreamVersion(id)` | Last event version of the stream (`null` if absent) |
| `FluxEvent` | Abstract base: `Id` (stream), `Version` (assigned by the store) |
| `[FluxEvent("Name")]` | Stable event type name persisted with each event |
| `[FluxProperty("Column")]` | Mark a property for persistence (Azure Table compatible types) |
| `FluxProjection` | Abstract base: implement `Apply(SomeEvent)` per event type; unhandled types are ignored |

## How storage works

One table (default `FluxStore`), partition key = stream id:

- **Header row** (`F-HEAD`) — tracks the stream's current version
- **Event rows** (`F-{guid}`) — `EventType` + `Version` + one column per `[FluxProperty]`

Events are written in a table transaction with the header update, so version never
advances without the event landing. Reads replay events in timestamp order.

For local development, use [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) with `UseDevelopmentStorage=true`:

```bash
azurite
dotnet run --project sample/SimpleFlux.Sample   # interactive demo (menu of 5 modules)
```

## Roadmap

Short term — hardening (semver `1.0.1`):
- Add a test project (event round-trip, versioning, projections, batch semantics)
- Restore `Version` on read; order reads by `Version` instead of timestamp
- Optimistic concurrency on version increments (ETag) and 100-entity batch chunking
- Stop swallowing all exceptions in header lookups

Later — features (`1.1.0`+):
- `CancellationToken` support across the API
- Snapshots for large streams
- Stream deletion/archival
- Metadata on events (correlation id, causation id)

Open source ideas are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Releasing

Prereleases and stable releases are published through GitHub Actions. See
[RELEASING.md](RELEASING.md) for the complete guide — including how to publish a
`1.1.0-alpha.1` prerelease and promote it to `1.1.0`.

## License

[MIT](LICENSE) © Cj Stremick