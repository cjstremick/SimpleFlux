# AGENTS.md

## Project Overview

**SimpleFlux** is a simple event-sourcing library for .NET. v1.0.0 was a single Azure Table Storage implementation; the repo is currently on the `feat/storage-abstraction` branch heading to a **2.0.0** release that introduces a storage-backend contract so the same `FluxStore` can run against Azure Tables, an in-memory store, or a third-party backend.

- **Owner:** Cj Stremick (personal / public repo)
- **NuGet:** `SimpleFlux` v1.0.0 (published Sept 2024). The **next package is 2.0.0** and is **breaking** (contract change: `FluxStore` now takes an `IStreamStore`) — do not ship anything against 1.x assumptions.
- **Last code activity:** Aug 2026 — the storage-abstraction #7/#5/#6 work plus cancellation-token support (#12) are now in. Expect no NuGet drift: the published package is still 1.0.0 while local is 2.0-unreleased.
- **Canonical architecture spec:** `docs/ARCHITECTURE.md` (see it before changing the contract). This file is the on-disk quick-start; ARCHITECTURE.md is the source of truth.

## Tech Stack

- **Language:** C# (ImplicitUsings enabled, Nullable enabled)
- **Target framework:** `net10.0` (single-target, current LTS)
- **Core runtime dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions` (interfaces only)
- **`SimpleFlux.AzureTables` dependency:** `Azure.Data.Tables` (12.11.0, pinned in `Directory.Packages.props`)
- **`SimpleFlux.InMemory` dependency:** none
- **Sample-only dependency:** `Faker.Net` (data generation in the demo)
- **Package version management:** Central Package Management (`Directory.Packages.props` + `Directory.Build.props`); `NUGET_API_KEY` secret in CI.

## Repo Structure (2.0.0)

```
SimpleFlux.sln
├── src/SimpleFlux/                  # Core: FluxStore, events, projections, DI (NuGet pkg SimpleFlux)
│   ├── FluxStore.cs                 # Storage-agnostic facade: append/read/project
│   ├── IStreamStore.cs              # The backend contract (Append/Read/GetMetadata)
│   ├── FluxEvent.cs                 # Abstract event base (Id, Version)
│   ├── FluxEventRecord.cs           # Core↔backend DTO (EventTypeName, Version, Properties)
│   ├── FluxEventAttribute.cs        # [FluxEvent("Name")]
│   ├── FluxPropertyAttribute.cs     # [FluxProperty("ColumnName")]
│   ├── FluxConcurrencyException.cs  # Concurrency conflict (streamId, expected, actual)
│   ├── FluxStreamMetadata.cs        # Stream metadata DTO (StreamId, Version)
│   ├── FluxOptions.cs               # EventAssemblies / EventTypes / StoreLifetime
│   ├── FluxBuilder.cs               # IFluxBuilder surface
│   ├── FluxBuilderExtensions.cs     # AddSimpleFlux()
│   └── FluxProjection.cs            # Abstract projection base (dynamic Apply dispatch)
├── src/SimpleFlux.AzureTables/      # Azure Table Storage backend (pkg SimpleFlux.AzureTables)
│   ├── AzureTableStreamStore.cs     # IStreamStore impl w/ ETag concurrency + Version ordering
│   ├── FluxHeader.cs                # Header-row ITableEntity (Version carrier)
│   └── FluxBuilderExtensions.cs     # UseAzureTables(TableClient)
├── src/SimpleFlux.InMemory/         # In-memory backend (pkg SimpleFlux.InMemory)
│   ├── InMemoryStreamStore.cs       # IStreamStore impl (lock-per-stream, no deps)
│   └── FluxBuilderExtensions.cs     # UseInMemory()
├── sample/SimpleFlux.Sample/        # Console menu demo (runs against InMemory by default)
│   ├── Events/                      # ItemAdded, ItemRemoved
│   ├── Modules/                     # WriteSingle, WriteBatch, WriteHybridBatch, Projection, LargeStream
│   └── Projections/                 # ItemInventoryProjection
├── benchmarks/SimpleFlux.Benchmarks/  # BenchmarkDotNet suite (branch bench/benchmark-suite)
│   ├── Benchmarks.cs                # Performance + safety scenarios
│   └── SafetyBenchmarks.cs          # Behavioral assertions (concurrency/read-order/version)
├── docs/
│   ├── ARCHITECTURE.md              # ⚠ AUTHORITATIVE — storage contract, packages, DI, breaking changes
│   └── BENCHMARKING.md              # Benchmark design (scenarios, metrics, CI plan)
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                   # Build + pack (verification only). Tests run only if a *Tests.csproj exists.
│   │   ├── publish-prerelease.yml   # Manual → prerelease to NuGet
│   │   └── release.yml              # Manual → promote prerelease → stable
│   └── dependabot.yml               # weekly nuget updates
├── RELEASING.md                     # ⚠ Read before publishing anything
├── CHANGELOG.md                     # Release log (2.0.0 storage abstraction is the headline)
├── CONTRIBUTING.md                  # Branching + workflow conventions
├── Directory.Build.props / Directory.Packages.props
├── SimpleFlux.sln
└── .vscode/                         # ⚠ STALE — launch.json/tasks.json reference "ConsoleApp1"
```

The 1.x mental model (`FluxStore` takes a `TableClient`, one project) is **gone**. The store now takes an `IStreamStore`. Use `UseInMemory()` for zero-setup dev/tests; `UseAzureTables(new TableClient(...))` for production.

## Build & Run

```bash
# Requires .NET 10 SDK. (On this dev Mac, `dotnet` is NOT installed yet — see "Setup notes".)
dotnet restore
dotnet build --configuration Release

# Sample runs against the in-memory store by default — no Azurite needed.
# To point it at Azure Tables, swap .UseInMemory() -> .UseAzureTables(new TableClient("UseDevelopmentStorage=true","FluxStore"))
# and run Azurite: docker run -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
dotnet run --project sample/SimpleFlux.Sample

# Benchmarks (branch bench/benchmark-suite):
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks
```

The sample is a menu app: 1 = single write, 2 = batch write (20 events, one stream), 3 = hybrid batch (20 events across 2 streams), 4 = projection demo, 5 = large stream (180 events) + project. Module 5 is the natural perf baseline — see `docs/BENCHMARKING.md`.

## Storage Model

**Core contract only.** Backends implement `IStreamStore`:

```csharp
Task AppendToStreamAsync(string streamId, int expectedVersion,
    IReadOnlyList<FluxEventRecord> events, int newVersion,
    CancellationToken ct = default);
Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken ct = default);
Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken ct = default);
```

`expectedVersion` semantics: `-2` (Any) append regardless; `-1` (NoStream) require stream to not exist yet; `>=0` exact match. On violation the backend throws `FluxConcurrencyException` **atomically (no partial writes)**. Core computes versions; backends enforce. First event in a stream is version **1**.

**Azure Tables backend specifics:**
- One table; `PartitionKey` = stream id; header row (`RowKey = "F-HEAD"`) carries the `Version`.
- Append reads the header, enforces `expectedVersion`, and writes event rows + header update as **one table transaction** (max 100 entities per transaction — see "Unchunked batch limit" below).
- `AppendToStreamAsync` enforces optimistic concurrency with the header **ETag** (412/409 → `FluxConcurrencyException(streamId, expectedVersion, actualVersion)`).
- `ReadStreamAsync` **orders by `Version`** (fixes the old timestamp-ordering instability).
- `GetStreamMetadataAsync` returns `null` on a 404; other failures propagate.

**`FluxEventRecord`** (core DTO): `EventTypeName` (the `[FluxEvent]` name), `Version`, and `Properties` (dictionary built from `[FluxProperty]`-attributed members). Backends never reflect on events — mapping is the core's job.

## Concurrency & Versioning

- **Optimistic concurrency is enforced at the backend via ETag** (#7, resolved). Concurrent appends that mismatch `expectedVersion` throw `FluxConcurrencyException` with the actual stream version, so callers can retry read-modify-write.
- The **core assigns versions**; the header ETag guards persistence. This removes the historical read-modify-write race that the 1.x "no concurrency safety" note described.
- `AddEvents` groups events by stream id and fans out one transaction per group via `Task.WhenAll`.

## Architecture & Conventions

1. **Events:** subclass `FluxEvent(id)`, decorate with `[FluxEvent("TypeName")]`, mark persisted fields with `[FluxProperty("ColumnName")]` (the column value is `property.GetValue(...)`). `Id` = stream/partition key; `Version` is assigned by the store and **is restored on read** (`Hydrate` sets `@event.Version = record.Version`).
2. **Event discovery:** `AddSimpleFlux().WithEvent<T>()` (exact type) or `.WithAssemblyEvents<TMarker>()` (all in an assembly). As soon as anything registers explicitly, the implicit "scan all loaded assemblies" fallback is disabled. Events must be discoverable in a registered assembly at store construction.
3. **Projections:** subclass `FluxProjection(id)` and implement `public void Apply(SomeEvent e)` per event type. `ApplyChange` routes via `dynamic`; a built-in no-op `Apply(FluxEvent)` swallows unhandled types. `FluxStore.ProjectTo<T>(id)` replays the stream and returns the projection, or `null` for an empty stream.
4. **DI:** `services.AddSimpleFlux().WithAssemblyEvents<ItemAdded>().UseInMemory()` (or `.UseAzureTables(...)`). No-DI still works: `new FluxStore(new InMemoryStreamStore())`.
5. **Conventions:** file-scoped namespaces, `public` API, full **XML docs** on the public surface (FluxStore, IStreamStore, FluxProjection, FluxEventRecord, FluxStreamMetadata, FluxConcurrencyException), nullable annotations on, cancellation tokens everywhere (#12).
6. **Versioning:** csproj uses `VersionPrefix` (local default 1.0.0). CI/publish workflows pass the exact version via `-p:Version=X.Y.Z` (stable) or `-p:Version=X.Y.Z-alpha.N` (prerelease). The 2.0.0 release is governed by the `ci/release-pipeline` CI path. Never hardcode a full version inline — see `RELEASING.md`.

## Release Pipeline (the three workflows)

1. **`ci.yml`** — automatic on every PR/push to main. Restore → build → pack (verification only, **packing does not publish**). The `Test` step is guarded by `if: hashFiles('**/*Tests.csproj') == ''` and is **skipped until a test project exists**.
2. **`publish-prerelease.yml`** — manual (Actions → "Publish Prerelease"). Takes e.g. `1.1.0-alpha.1`, validates semver, checks NuGet for prior existence, packs, pushes prerelease, creates a matching GitHub **Pre-release** + git tag `vX.Y.Z`.
3. **`release.yml`** — manual (Actions → "Publish Release"). Promotes a prerelease (`1.1.0-alpha.1` → `1.1.0`) or releases a stable version directly. Creates the GitHub Release + tag.

**Hard requirements:** `NUGET_API_KEY` secret in repo Settings → Secrets and variables → Actions. All publishing workflows fail fast with a clear message if missing. **All three `SimpleFlux*` packages ship together at the same version** from one pack (Central Package Management keeps dep versions aligned; the version check guards the core package).

## Known Issues & Tech Debt

- **No tests yet (#9, not implemented).** No test project in the solution; the CI test step is a no-op placeholder (`hashFiles('**/*Tests.csproj') == ''`). The In-Memory store exists *specifically* to enable a parameterized test suite over InMemory + AzureTables, but it hasn't been written. The In-Memory store is also the basis for the safety benchmarks (branch `bench/benchmark-suite`).
- **Unchunked batch limit.** `AppendToStreamAsync` submits events + header as one Azure table transaction (max 100 entities). `FluxStore.AddEvents` groups by stream id across `Task.WhenAll` but does **not** chunk groups larger than 100 — a batch >99 events to one stream fails server-side. (The LargeStream sample writes 180 to one stream via `AddEvents`, so it relies on a single write; worth a guard or chunking.) The performance benchmarks target this cliff explicitly (batch sweep to 125).
- **Reflection/dynamic dispatch is a perf hotspot.** Event discovery (assembly scan), record hydration/mapping (`GetProperties` per event), and projection dispatch (`(dynamic)this).Apply((dynamic)@event)`) all hit reflection. The `dynamic` cast in particular is a candidate for being slower than a hand-rolled dispatch and will show up in the reflection benchmark.
- **Header lookup still 404-swallows (#6, partially in progress).** `GetStreamMetadataAsync` and `ReadHeaderAsync` treat a 404 as "no stream" (returns null) — now intentional per the contract — but there's no separate distinction for auth/network failures at the `FluxStore` level today.
- **Stale `.vscode/`** — `launch.json`/`tasks.json` reference `ConsoleApp1` from the original template. Needs regenerating or removing.
- **README is thin** — no API docs / package-usage example. The XML docs on the public API are the best current reference; README should be refreshed ahead of 2.0.0.
- **`LargeStreamModule` sample has stale typos/dead code** — `Desciption` (missing `r`), `quatity`, and a leftover `if (true)` block. Pre-2.0.0 cleanup debt.
- **No tags/releases on GitHub yet.** NuGet shipped 1.0.0 without a GitHub release. The `ci/release-pipeline` workflows create tags/releases going forward, but older history includes a few terse placeholder commits.

## Resolved (was in the old "Known Issues", now fixed — kept here so stale notes don't get re-added)

- ✅ **Concurrency safety (#7).** The old 1.x note "no ETag/optimistic-concurrency; concurrent writers produce duplicate versions" is obsolete — see `AppendToStreamAsync` ETag enforcement above.
- ✅ **Read ordering (#5).** `ReadStreamAsync` now orders by `Version`, not `TableEntity.Timestamp`.
- ✅ **`Version` not restored on read (# old).** `Hydrate` now restores `@event.Version = record.Version`.
- ✅ **XML docs on the public API.** The public surface is now fully XML-documented.
- ✅ **Single-project structure.** Replaced by the 3-package + `IStreamStore` contract layout (this whole rewrite is the 2.0.0 change).

## Benchmarking

See **`docs/BENCHMARKING.md`** — benchmark design tracked on branch `bench/benchmark-suite`. Summary:

- **`benchmarks/SimpleFlux.Benchmarks`** — `BenchmarkDotNet` (0.15.8, pinned in CPM) suite, parameterized over **InMemory vs AzureTables** so the same matrix runs offline and against real storage (Azurite).
- **Performance:** single-append latency, batch-append throughput vs batch size (the 100-entity cliff is a first-class target), stream read, projection replay over a 180-event stream (the `LargeStreamModule` baseline), 8-writer concurrency contention, and reflection/discovery + hydration cost.
- **Safety:** behavioral asserts — concurrent writers raise `FluxConcurrencyException` with the correct `ActualVersion`; reads come back in version order; an `Apply(FluxEvent)`-only projection ignores unknown events without throwing; and `Version` is restored on read. These are regression guards for the bugs that were fixed in 2.0.0.
- **CI future:** gated job running InMemory on PRs, AzureTables on main; compare against the last green run for drift.
- **Caveat:** the benchmark project has **not been build-verified** — no .NET 10 SDK is installed on this dev Mac. Install the SDK and run `dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks`.

## Getting Work Done Here

- **Prefer the In-Memory store for local iteration** — `new FluxStore(new InMemoryStreamStore())` or `.UseInMemory()` runs anywhere, no Azurite, no network. Use it for the sample, benchmarks, and tests; switch to Azure Tables only to validate real-storage behavior.
- **Keep the storage contract sacred** — `IStreamStore` is the 2.0.0 breaking change. Before altering it, read `docs/ARCHITECTURE.md` and check `feat/cancellation-tokens` for precedent.
- **Add tests before new features** — #9 is the biggest gap. The In-Memory store makes a CI-runnable, emulator-free test suite finally feasible.
- **Fix the 100-entity batch cliff and stale sample typos** before 2.0.0 ships.
- **Versioning: never hardcode** — go through the publish workflows (`RELEASING.md`). The 2.0.0 release is the priority gate.
- The library targets `net10.0` only; if net8 consumers ever matter, multi-target `net8.0;net10.0`.

## Setup Notes (dev Mac)

- `.NET 10 SDK` is not installed (`dotnet: command not found` as of Aug 2026). Install via:
  - `brew install --cask dotnet` (SDK), or the `dotnet-install.sh` script pinned to 10.0.x.
  - Required for: local build, running the sample, the benchmark project, and tests.
- Python on this Mac shadows some Homebrew tooling; `.NET` has no such conflict, but keep `dotnet` on `PATH` after install (the installer notes a shell-profile tweak).
