# SimpleFlux Architecture — Storage Backend Abstraction

Status: accepted (2026-08). Design for SimpleFlux **2.0.0**.

## Goal

SimpleFlux (core) provides the event-sourcing infrastructure, interfaces, and
abstractions and knows **nothing** about any storage technology. Storage backends
are separate NuGet packages that implement a single storage contract. This document
describes the contract, the package layout, the reference implementations, and the
DI story.

## Package layout

All packages share the same version and are published together.

| Package | Contents | Dependencies |
|---|---|---|
| `SimpleFlux` (core) | `FluxStore` facade, `FluxEvent`, `[FluxEvent]`, `[FluxProperty]`, `FluxProjection`, event discovery/hydration, `IStreamStore`, `FluxEventRecord`, `FluxStreamMetadata`, `FluxConcurrencyException`, `FluxOptions`, `AddSimpleFlux()` + `IFluxBuilder` | `Microsoft.Extensions.DependencyInjection.Abstractions` (interfaces only) |
| `SimpleFlux.AzureTables` | `AzureTableStreamStore` + `UseAzureTables()` | `SimpleFlux`, `Azure.Data.Tables` |
| `SimpleFlux.InMemory` | `InMemoryStreamStore` + `UseInMemory()` | `SimpleFlux` only |

Namespaces mirror package names (`SimpleFlux`, `SimpleFlux.AzureTables`,
`SimpleFlux.InMemory`).

## The storage contract

Backends implement one interface. The contract is intentionally small.

```csharp
namespace SimpleFlux;

public interface IStreamStore
{
    Task AppendToStreamAsync(string streamId, int expectedVersion,
        IReadOnlyList<FluxEventRecord> events, int newVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId,
        CancellationToken cancellationToken = default);

    Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId,
        CancellationToken cancellationToken = default);
}
```

### Version semantics

- `expectedVersion` is the stream version the caller believes is current:
  - `-2` (`Any`) — append regardless of current version
  - `-1` (`NoStream`) — append only if the stream does **not** exist yet
  - `>= 0` — append only if the stream's current version **equals** this value
- On violation the backend throws `FluxConcurrencyException` **atomically** (no
  partial writes).
- `newVersion` is the header/stream version after this append (last event version).
  The **core** computes versions; backends enforce and persist. This keeps version
  behavior identical across backends and fixes the historical read-modify-write race.
- First event in a stream is version **1**.

### Data shape

Backends never reflect on events. The core converts `FluxEvent` ↔ `FluxEventRecord`
(discovery, attributes, hydration all live in core):

```csharp
public sealed class FluxEventRecord
{
    public required string EventTypeName { get; init; }
    public int Version { get; init; }
    public required IReadOnlyDictionary<string, object?> Properties { get; init; }
}

public sealed class FluxStreamMetadata
{
    public required string StreamId { get; init; }
    public int Version { get; init; }
}
```

`Properties` is populated from `[FluxProperty]`-attributed members. Values must be
types the backend can store (backend-specific mapping is the backend's business —
Azure Tables maps to columns, a JSON backend serializes, InMemory stores as-is).

## Reference implementations

### SimpleFlux.AzureTables

- One table; partition key = stream id; header row (`F-HEAD`) carries the version.
- `AppendToStreamAsync`: reads the header, enforces `expectedVersion` (helper
  check for a clear error + **ETag-conditional header update** for bulletproof
  optimistic concurrency — a `412`/`409` becomes `FluxConcurrencyException`), then
  submits events + header as one table transaction.
- `ReadStreamAsync`: `QueryAsync` filtered to the stream, **ordered by `Version`**
  (fixes the old timestamp-ordering instability).
- `GetStreamMetadataAsync`: header read; a 404 → `null`; cancellation and other
  failures propagate (part of the issue #6 cleanup).

### SimpleFlux.InMemory

- Zero dependencies, lock-per-stream dictionaries, same atomicity + conflict
  semantics.
- Purpose: pattern proof, quickstarts, offline demos, and the **test store** for
  the test suite (CI can run tests with no emulator).

## DI & registration

```csharp
// Core: fluent builder
services.AddSimpleFlux()
        .WithAssemblyEvents<ItemAdded>()        // events from this assembly
        // .WithEvent<ItemAdded>()              // or: exactly one event type
        .UseInMemory();                         // SimpleFlux.InMemory

// or Azure Tables (SimpleFlux.AzureTables):
services.AddSimpleFlux()
        .WithAssemblyEvents<ItemAdded>()
        .UseAzureTables(new TableClient("UseDevelopmentStorage=true", "FluxStore"));

// no-DI usage still works:
var store = new FluxStore(new InMemoryStreamStore());
```

- `AddSimpleFlux(Action<FluxOptions>?)` lives in core (depends only on
  `Microsoft.Extensions.DependencyInjection.Abstractions` — no runtime provider).
- Event registration is fluent: `WithEvent<T>()` (one type, no scanning),
  `WithAssemblyEvents<TMarker>()` / `WithAssemblyEvents(Assembly)` (all events in an
  assembly). As soon as anything is registered explicitly, the implicit
  "scan all loaded assemblies" fallback is disabled.
- `FluxOptions`:
  - `EventTypes` / `EventAssemblies` — the explicit registrations above.
  - `StoreLifetime` — DI lifetime for the store + `FluxStore` (default
    `Singleton`; both are stateless after construction).
- `IFluxBuilder` is the extension point: each backend package adds a
  `.Use<Backend>()` extension that registers its `IStreamStore` implementation.
  Third-party backends use the same hook.

## Breaking changes vs 1.x

- `FluxStore` constructor takes `IStreamStore` (+ optional `FluxOptions`) instead
  of `TableClient`. **This is the reason for 2.0.0.**
- `FluxHeader` (an Azure `ITableEntity`) moves out of core into
  `SimpleFlux.AzureTables`; core exposes `FluxStreamMetadata`.
- Everything else on the consumer surface (`AddEvent`, `AddEvents`, `ProjectTo`,
  `StreamExists`, `GetStreamVersion`, event attributes, projections, cancellation
  tokens) is unchanged.

## Publishing (multi-package)

All three packages publish together at the same version from one workflow run:
pack the solution once, push every `SimpleFlux*.nupkg`/`.snupkg`, attach the
`.nupkg` files to the GitHub release. The version check guards the core package.
Central Package Management (`Directory.Packages.props`) keeps dependency versions
in one file.

## Roadmap alignment

- Fixes #7 (concurrency) by contract design.
- Fixes #5 (stability of read order) in the AzureTables backend.
- Part of #6 (header lookups no longer swallow non-404 failures) in backends.
- Enables #9 (tests) with a parameterized suite over InMemory + AzureTables.