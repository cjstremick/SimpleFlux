# AGENTS.md

## Project Overview

**SimpleFlux** is a simple event-sourcing library for .NET. v1.0.0 was a single Azure Table Storage implementation; the repo is currently on the `bench/benchmark-suite` branch heading to a **2.0.0** release that introduces a storage-backend contract so the same `FluxStore` can run against Azure Table Storage, flat files, or an in-memory store.

- **Owner:** Cj Stremick (personal / public repo)
- **NuGet:** `SimpleFlux` v1.0.0 (published Sept 2024). The **next package is 2.0.0** and is **breaking** (contract change: `FluxStore` now takes an `IStreamStore`; Version changed from `int` to `long`) — do not ship anything against 1.x assumptions.
- **Last code activity:** Sept 2026 — FlatFile backend, Version→long, chunked transactions, collision guard, contract tests, benchmarks across all 3 backends.
- **Canonical architecture spec:** `docs/ARCHITECTURE.md` (see it before changing the contract). This file is the on-disk quick-start; ARCHITECTURE.md is the source of truth.

## Tech Stack

- **Language:** C# (ImplicitUsings enabled, Nullable enabled)
- **Target framework:** `net10.0` (single-target, current LTS)
- **SDK version:** pinned in `global.json` (10.0.400, rollForward: latestFeature)
- **Core runtime dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions` (interfaces only)
- **`SimpleFlux.AzureTables` dependency:** `Azure.Data.Tables` (12.11.0, pinned in `Directory.Packages.props`)
- **`SimpleFlux.InMemory` dependency:** none
- **`SimpleFlux.FlatFile` dependency:** none (uses `System.Text.Json` from runtime)
- **Sample-only dependency:** `Faker.Net` (data generation in the demo)
- **Test framework:** xUnit 2.9.3 (pinned in CPM)
- **Package version management:** Central Package Management (`Directory.Packages.props` + `Directory.Build.props` + `Directory.Build.targets`); `NUGET_API_KEY` secret in CI.

## Repo Structure (2.0.0)

```
SimpleFlux.sln
├── src/SimpleFlux/                  # Core: FluxStore, events, projections, DI (NuGet pkg SimpleFlux)
│   ├── FluxStore.cs                 # Storage-agnostic facade: append/read/project
│   ├── IStreamStore.cs              # The backend contract (Append/Read/GetMetadata) — long versions
│   ├── FluxEvent.cs                 # Abstract event base (Id, Version: long)
│   ├── FluxEventRecord.cs           # Core↔backend DTO (EventTypeName, Version: long, Properties)
│   ├── FluxEventAttribute.cs        # [FluxEvent("Name")]
│   ├── FluxPropertyAttribute.cs     # [FluxProperty("ColumnName")]
│   ├── FluxConcurrencyException.cs  # Concurrency conflict (streamId, expected, actual — all long)
│   ├── FluxStreamMetadata.cs        # Stream metadata DTO (StreamId, Version: long)
│   ├── FluxOptions.cs               # EventAssemblies / EventTypes / StoreLifetime
│   ├── FluxBuilder.cs               # IFluxBuilder surface
│   ├── FluxBuilderExtensions.cs     # AddSimpleFlux()
│   └── FluxProjection.cs            # Abstract projection base (dynamic Apply dispatch)
├── src/SimpleFlux.AzureTables/      # Azure Table Storage backend (pkg SimpleFlux.AzureTables)
│   ├── AzureTableStreamStore.cs     # IStreamStore impl: ETag concurrency, chunked txns (≤99 events/txn), collision guard
│   ├── FluxHeader.cs                # Header-row ITableEntity (Version: long)
│   └── FluxBuilderExtensions.cs     # UseAzureTables(TableClient)
├── src/SimpleFlux.InMemory/         # In-memory backend (pkg SimpleFlux.InMemory)
│   ├── InMemoryStreamStore.cs       # IStreamStore impl (lock-per-stream, no deps)
│   └── FluxBuilderExtensions.cs     # UseInMemory()
├── src/SimpleFlux.FlatFile/         # Flat-file backend (pkg SimpleFlux.FlatFile)
│   ├── FlatFileStreamStore.cs       # IStreamStore impl (JSONL per stream, file locking, zero deps)
│   └── FluxBuilderExtensions.cs     # UseFlatFile(rootDirectory)
├── sample/SimpleFlux.Sample/        # Console menu demo (runs against InMemory by default)
│   ├── Events/                      # ItemAdded, ItemRemoved
│   ├── Modules/                     # WriteSingle, WriteBatch, WriteHybridBatch, Projection, LargeStream
│   └── Projections/                 # ItemInventoryProjection
├── tests/SimpleFlux.Tests/          # Contract test suite (xUnit, parameterized over backends)
│   └── Contract/
│       ├── StreamStoreContractTests.cs   # Shared contract: 14 scenarios, base class
│       ├── InMemoryContractTests.cs      # InMemory runner
│       ├── FlatFileContractTests.cs      # FlatFile runner (temp dir cleanup)
│       └── AzureTablesContractTests.cs   # AzureTables runner (needs Azurite)
├── benchmarks/SimpleFlux.Benchmarks/  # BenchmarkDotNet suite (branch bench/benchmark-suite)
│   ├── Benchmarks.cs                # Performance + safety scenarios, 3 backends × 7 batch sizes
│   └── SafetyBenchmarks.cs          # Behavioral assertions (concurrency/read-order/version)
├── docs/
│   ├── ARCHITECTURE.md              # ⚠ AUTHORITATIVE — storage contract, packages, DI, breaking changes
│   └── BENCHMARKING.md              # Benchmark design (scenarios, metrics, CI plan)
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                   # Build + pack (verification only). Tests run only if a *Tests.csproj exists.
│   │   ├── publish-prerelease.yml   # Manual → prerelease to NuGet
│   │   └── release.yml              # Manual → promote prerelease → stable
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   └── feature_request.yml
│   └── dependabot.yml               # weekly nuget updates
├── global.json                      # SDK version pin (10.0.400, rollForward: latestFeature)
├── Directory.Build.props            # Shared NuGet metadata, ImplicitUsings, Nullable
├── Directory.Build.targets          # TreatWarningsAsErrors, test project defaults
├── Directory.Packages.props         # Central Package Management (all versions in one file)
├── RELEASING.md                     # ⚠ Read before publishing anything
├── CHANGELOG.md                     # Release log (2.0.0 storage abstraction is the headline)
├── CONTRIBUTING.md                  # Branching + workflow conventions
├── CONTRIBUTORS.md                  # Maintainer + contribution guide
├── README.md                        # Adoption-focused quickstart, backend docs, custom backend guide
└── .vscode/                         # ⚠ STALE — launch.json/tasks.json reference "ConsoleApp1"
```

The 1.x mental model (`FluxStore` takes a `TableClient`, one project) is **gone**. The store now takes an `IStreamStore`. Use `UseInMemory()` for zero-setup dev/tests; `UseFlatFile(path)` for local persistence; `UseAzureTables(new TableClient(...))` for production.

## Build & Run

```bash
# Requires .NET 10 SDK (pinned in global.json — SDK 10.0.400+).
dotnet restore
dotnet build --configuration Release

# Tests (InMemory + FlatFile — no Azurite needed)
dotnet test

# Tests including Azure Tables (needs Azurite)
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
dotnet test
docker stop azurite && docker rm azurite

# Sample (InMemory by default — no setup needed)
dotnet run --project sample/SimpleFlux.Sample

# Benchmarks (3 backends × 7 batch sizes — Azurite needed for AzureTables)
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*'
docker stop azurite && docker rm azurite
```

The sample is a menu app: 1 = single write, 2 = batch write (20 events, one stream), 3 = hybrid batch (20 events across 2 streams), 4 = projection demo, 5 = large stream (180 events) + project. Module 5 is the natural perf baseline — see `docs/BENCHMARKING.md`.

## Storage Model

**Core contract only.** Backends implement `IStreamStore`:

```csharp
Task AppendToStreamAsync(string streamId, long expectedVersion,
    IReadOnlyList<FluxEventRecord> events, long newVersion,
    CancellationToken ct = default);
Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken ct = default);
Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken ct = default);
```

`expectedVersion` semantics: `-2` (Any) append regardless; `-1` (NoStream) require stream to not exist yet; `>=0` exact match. On violation the backend throws `FluxConcurrencyException` **atomically (no partial writes)**. Core computes versions; backends enforce. First event in a stream is version **1**.

**Azure Tables backend specifics:**
- One table; `PartitionKey` = stream id; header row (`RowKey = "F-HEAD"`) carries the `Version`.
- Append reads the header, enforces `expectedVersion`, and writes event rows + header update as table transactions of **≤100 entities each** (larger batches are chunked; each chunk is atomic but the full batch is not).
- `AppendToStreamAsync` enforces optimistic concurrency with the header **ETag** (412/409 → `FluxConcurrencyException(streamId, expectedVersion, actualVersion)`).
- `ReadStreamAsync` **orders by `Version`** (fixes the old timestamp-ordering instability).
- `GetStreamMetadataAsync` returns `null` on a 404; other failures propagate.
- **Reserved column name guard:** event properties using `[FluxProperty]` names that collide with Azure columns (`PartitionKey`, `RowKey`, `Timestamp`, `ETag`, `EventType`, `Version`, `F-HEAD`) throw `ArgumentException` at write time.

**FlatFile backend specifics:**
- Each stream gets its own directory: `{root}/{streamId}/events.jsonl` + `meta.json`.
- Appends are file-level: events are serialized as JSON lines and appended to `events.jsonl`. Metadata is updated via temp file + `File.Move` (crash-safe).
- Per-stream file locking via exclusive `FileStream` on `.lock`.
- JSON deserialization converts `JsonElement` values back to native .NET types (`int`, `long`, `string`, `bool`, `double`) for correct hydration.

**`FluxEventRecord`** (core DTO): `EventTypeName` (the `[FluxEvent]` name), `Version` (long), and `Properties` (dictionary built from `[FluxProperty]`-attributed members). Backends never reflect on events — mapping is the core's job.

## Concurrency & Versioning

- **Optimistic concurrency is enforced at the backend via ETag** (#7, resolved). Concurrent appends that mismatch `expectedVersion` throw `FluxConcurrencyException` with the actual stream version, so callers can retry read-modify-write.
- The **core assigns versions**; the header ETag guards persistence. This removes the historical read-modify-write race that the 1.x "no concurrency safety" note described.
- `AddEvents` groups events by stream id and fans out one transaction per group via `Task.WhenAll`.
- **Version is `long`** across the entire contract (breaking change from 2.0.0).

## Architecture & Conventions

1. **Events:** subclass `FluxEvent(id)`, decorate with `[FluxEvent("TypeName")]`, mark persisted fields with `[FluxProperty("ColumnName")]` (the column value is `property.GetValue(...)`). `Id` = stream/partition key; `Version` (long) is assigned by the store and **is restored on read** (`Hydrate` sets `@event.Version = record.Version`).
2. **Event discovery:** `AddSimpleFlux().WithEvent<T>()` (exact type) or `.WithAssemblyEvents<TMarker>()` (all in an assembly). As soon as anything registers explicitly, the implicit "scan all loaded assemblies" fallback is disabled. Events must be discoverable in a registered assembly at store construction.
3. **Projections:** subclass `FluxProjection(id)` and implement `public void Apply(SomeEvent e)` per event type. `ApplyChange` routes via `dynamic`; a built-in no-op `Apply(FluxEvent)` swallows unhandled types. `FluxStore.ProjectTo<T>(id)` replays the stream and returns the projection, or `null` for an empty stream.
4. **DI:** `services.AddSimpleFlux().WithAssemblyEvents<ItemAdded>().UseInMemory()` (or `.UseFlatFile(path)` / `.UseAzureTables(...)`). No-DI still works: `new FluxStore(new InMemoryStreamStore())`.
5. **Conventions:** file-scoped namespaces, `public` API, full **XML docs** on the public surface, nullable annotations on, cancellation tokens everywhere (#12).
6. **Versioning:** csproj uses `VersionPrefix` (local default 2.0.0). CI/publish workflows pass the exact version via `-p:Version=X.Y.Z` (stable) or `-p:Version=X.Y.Z-alpha.N` (prerelease). Never hardcode a full version inline — see `RELEASING.md`.

## Test Suite

Contract tests (`tests/SimpleFlux.Tests`) exercise the `IStreamStore` contract against every backend via inheritance:

- `StreamStoreContractTests` — abstract base with 14 scenarios (append, read, metadata, concurrency, FluxStore integration)
- `InMemoryContractTests` — runs against InMemory (no setup)
- `FlatFileContractTests` — runs against FlatFile (temp dir, cleaned up after)
- `AzureTablesContractTests` — runs against Azure Tables (needs Azurite)

Run with `dotnet test`. Azure Tables tests can be excluded with `--filter "FullyQualifiedName!~AzureTables"`.

## Benchmark Suite

Parameterized over 3 backends (InMemory, AzureTables, FlatFile) × 7 batch sizes (1, 10, 25, 50, 75, 100, 125):

| Scenario | What it measures |
|---|---|
| `Append_SingleEvent` | Single-event write latency |
| `Append_Batch` | Batch write throughput vs size |
| `Read_Stream` | 180-event stream read + projection |
| `Project_Stream` | 180-event projection replay |
| `Concurrency_Contention` | 8-writer collision rate |
| `Reflection_Overhead` | Event discovery + hydration cost |

Azure Tables benchmarks need Azurite. Results from Apple M4 (Sept 2026):
- InMemory: 1.3 µs single write, linear scaling
- FlatFile: 200 µs single write, ~1.5× at batch=125 (flat)
- AzureTables: 571 µs single write, 13-16 ms at batch=75-125 (chunking overhead)

## Release Pipeline (the three workflows)

1. **`ci.yml`** — automatic on every PR/push to main. Restore → build → pack (verification only, **packing does not publish**).
2. **`publish-prerelease.yml`** — manual (Actions → "Publish Prerelease"). Takes e.g. `1.1.0-alpha.1`, validates semver, checks NuGet for prior existence, packs, pushes prerelease, creates a matching GitHub **Pre-release** + git tag `vX.Y.Z`.
3. **`release.yml`** — manual (Actions → "Publish Release"). Promotes a prerelease (`1.1.0-alpha.1` → `1.1.0`) or releases a stable version directly. Creates the GitHub Release + tag.

**Hard requirements:** `NUGET_API_KEY` secret in repo Settings → Secrets and variables → Actions. All publishing workflows fail fast with a clear message if missing. **All three `SimpleFlux*` packages ship together at the same version** from one pack.

## Known Issues & Tech Debt

- **Reflection/dynamic dispatch is a perf hotspot.** Event discovery (assembly scan), record hydration/mapping (`GetProperties` per event), and projection dispatch (`(dynamic)this).Apply((dynamic)@event)`) all hit reflection. The `dynamic` cast in particular is a candidate for being slower than a hand-rolled dispatch and will show up in the reflection benchmark.
- **Stale `.vscode/`** — `launch.json`/`tasks.json` reference `ConsoleApp1` from the original template. Needs regenerating or removing.
- **`LargeStreamModule` sample has stale typos/dead code** — `Desciption` (missing `r`), `quatity`, and a leftover `if (true)` block. Pre-2.0.0 cleanup debt.
- **CHANGELOG needs updating** — the `[Unreleased]` section doesn't mention FlatFile, Version→long, chunking, collision guard, contract tests, or the 3-backend benchmark suite.

## Resolved

- ✅ **Concurrency safety (#7).** ETag enforcement in all backends.
- ✅ **Read ordering (#5).** `ReadStreamAsync` orders by `Version`, not timestamp.
- ✅ **`Version` not restored on read.** `Hydrate` restores `@event.Version = record.Version`.
- ✅ **XML docs on the public API.** Fully documented.
- ✅ **Single-project structure.** Replaced by 4-package + `IStreamStore` contract layout.
- ✅ **100-entity batch limit.** AzureTables backend now chunks batches into ≤99-event transactions.
- ✅ **Version int → long.** Full contract change across all backends.
- ✅ **Reserved column name collisions.** Guard in AzureTableStreamStore.ToEntity.
- ✅ **FlatFile backend.** JSONL per stream, file locking, zero deps.
- ✅ **Contract test suite.** 14 scenarios, parameterized over InMemory + FlatFile (AzureTables with Azurite).
- ✅ **Benchmark suite.** 3 backends × 7 batch sizes, perf + safety scenarios.

## Getting Work Done Here

- **Prefer the In-Memory store for local iteration** — no Azurite, no network. Use it for the sample, benchmarks, and tests; switch to FlatFile or Azure Tables only to validate real-storage behavior.
- **Keep the storage contract sacred** — `IStreamStore` is the 2.0.0 breaking change. Before altering it, read `docs/ARCHITECTURE.md`.
- **Run tests before committing** — `dotnet test` (InMemory + FlatFile is fast, no setup).
- **Versioning: never hardcode** — go through the publish workflows (`RELEASING.md`).
- The library targets `net10.0` only; if net8 consumers ever matter, multi-target `net8.0;net10.0`.

## Setup Notes (dev Mac)

- `.NET 10 SDK` is pinned in `global.json` (10.0.400). The SDK lives at `~/.dotnetsdk` — add to PATH:
  ```bash
  export PATH="$HOME/.dotnetsdk:$PATH"
  export DOTNET_ROOT="$HOME/.dotnetsdk"
  ```
- Azurite for Azure Tables testing: `docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite`
