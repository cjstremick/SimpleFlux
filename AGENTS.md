# AGENTS.md

## Project Overview

**SimpleFlux** is a simple event-sourcing library for .NET backed by **Azure Table Storage**. It is inspired by the (abandoned) Streamstone project. The library stores events as rows in a single Azure Table, tracks per-stream versions via a header row, and rebuilds projections by replaying events through reflection-based dispatch.

- **Owner:** Cj Stremick (personal / public repo)
- **Package on NuGet:** `SimpleFlux` v1.0.0 (published Sept 2024)
- **Last code activity:** Sept 2024 — expect package/dependency drift.

## Tech Stack

- **Language:** C# (ImplicitUsings enabled, Nullable enabled)
- **Target framework:** `net8.0` (⚠ .NET 8 EOL is Nov 2026 — migration to .NET 10 LTS should be planned)
- **Only runtime dependency:** `Azure.Data.Tables` **12.8.3** (latest is **12.11.0** — 3 minors behind; a stale dependabot PR for 12.9.1 is still open)
- **Sample-only dependency:** `Faker.Net` 2.0.163 (current)

## Repo Structure

```
SimpleFlux.sln
├── src/SimpleFlux/                  # The library (NuGet package)
│   ├── SimpleFlux.csproj            # net8.0, packable, PackageId=SimpleFlux
│   ├── FluxStore.cs                 # Main facade: CRUD events, projections, stream metadata
│   ├── FluxEvent.cs                 # Abstract event base (Id, Version)
│   ├── FluxEventAttribute.cs        # [FluxEvent("Name")] — event type name
│   ├── FluxPropertyAttribute.cs     # [FluxProperty("Name")] — property → column mapping
│   ├── FluxHeader.cs                # Stream header row entity (tracks Version)
│   └── FluxProjection.cs            # Abstract projection base (dynamic Apply dispatch)
├── sample/SimpleFlux.Sample/        # Console demo app (menu of 5 modules)
│   ├── Events/                      # ItemAdded, ItemRemoved (example events)
│   ├── Modules/                     # WriteSingle, WriteBatch, WriteHybridBatch, Projection, LargeStream
│   └── Projections/                 # ItemInventoryProjection
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                   # Build + test on every PR/push to main (no publishing)
│   │   ├── publish-prerelease.yml   # Manual: build + publish X.Y.Z-prerelease.N to NuGet
│   │   └── release.yml              # Manual: promote a prerelease (or release directly) as stable
│   └── dependabot.yml               # weekly nuget updates
├── RELEASING.md                     # Full release guide — read before publishing anything
└── .vscode/                         # ⚠ STALE — references nonexistent "ConsoleApp1"
```

## Build & Run

```bash
# Requires .NET SDK (not currently installed on the dev Mac — install .NET 8 or 10 SDK)
dotnet restore
dotnet build --configuration Release

# Run the sample (requires Azure Storage emulator — Azurite — for "UseDevelopmentStorage=true")
azurite  # or: docker run -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
dotnet run --project sample/SimpleFlux.Sample
```

The sample is a menu app: 1 = single write, 2 = batch write (20 events, one stream), 3 = hybrid batch (20 events across 2 streams), 4 = projection demo, 5 = large stream (180 events) + project.

## Storage Model

Everything lives in **one Azure Table** (default table name `FluxStore`):

- **PartitionKey** = stream id (the `FluxEvent.Id`)
- **Header row:** RowKey = `F-HEAD` — carries the stream's current `Version`
- **Event rows:** RowKey = `F-{Guid}` — columns include `EventType` (the `[FluxEvent]` name), `Version`, plus one column per `[FluxProperty]`-attributed property
- Writes use `TableTransactionAction` batches (event + header update in one transaction) — max 100 entities per transaction
- `FluxStore.AddEvents` groups incoming events by stream id and fans out one transaction per group via `Task.WhenAll`

## Architecture & Conventions

1. **Events:** subclass `FluxEvent(id)`, decorate with `[FluxEvent("TypeName")]`, and mark persisted fields with `[FluxProperty("ColumnName")]`. The `Id` is the stream/partition key; `Version` is assigned by the store.
2. **Event discovery:** `FluxStore`'s constructor scans all loaded assemblies for `FluxEvent` subclasses via reflection and maps `[FluxEvent]` names ↔ CLR types. Event types must be discoverable in an assembly loaded at store construction time.
3. **Projections:** subclass `FluxProjection(id)` and implement `public void Apply(SomeEvent e)` per event type. `ApplyChange` dispatches via `dynamic` — a no-op `Apply(FluxEvent)` fallback swallows unhandled event types. `FluxStore.ProjectTo<T>(id)` replays the stream and returns the projection (or `null` for an empty stream).
4. **Conventions:** file-scoped namespaces, `public` API, nullable annotations on.
5. **Versioning:** the csproj defines only `VersionPrefix` (local default 1.0.0). CI passes the exact version at pack time via `-p:Version=X.Y.Z` (stable) or `-p:Version=X.Y.Z-alpha.N` (prerelease). Never hardcode a full version — see RELEASING.md.

## Release Pipeline (the three workflows)

1. **`ci.yml`** — automatic. Runs on every PR and push to main. Restore → build → pack (verification only). Publishes nothing.
2. **`publish-prerelease.yml`** — manual (Actions tab → "Publish Prerelease" → Run workflow). Takes a version like `1.1.0-alpha.1`, validates semver, checks NuGet doesn't already have it, builds/packs with that version, pushes the prerelease package to NuGet, and creates a matching GitHub **Pre-release** + tag (`v1.1.0-alpha.1`).
3. **`release.yml`** — manual (Actions tab → "Publish Release" → Run workflow). Takes a prerelease version (e.g. `1.1.0-alpha.1`) and **promotes** it: verifies the prerelease exists on NuGet, strips the suffix → `1.1.0`, publishes the stable package, creates the GitHub Release + tag (`v1.1.0`). Passing a stable version directly (`1.1.0`) releases without a prerelease step.

**Hard requirements:** `NUGET_API_KEY` secret must exist in repo Settings → Secrets and variables → Actions (create at nuget.org → API Keys). All publish workflows fail fast with a clear message if it's missing. NuGet versions are immutable — a re-push of an existing version fails by design.

## Known Issues & Tech Debt

- **No tests at all** — no test project in the solution. Event round-trip, versioning, and concurrency are unverified.
- **`Version` is never restored on read:** `ToFluxEvent` only sets `[FluxProperty]` columns; the event's `Version` property stays 0 after load (it IS stored, just not deserialized).
- **Read ordering by timestamp:** `GetEvents` orders by `TableEntity.Timestamp`, not `Version` — batch-written events can share a timestamp and come back in unstable order.
- **`GetHeader` swallows all exceptions** and returns null — auth failures/network errors are indistinguishable from "stream doesn't exist".
- **Version increment is not concurrency-safe:** header read + increment + write has no ETag/optimistic-concurrency check — concurrent writers can produce duplicate versions/lost updates.
- **Batch size:** no chunking over the 100-entity Azure transaction limit.
- **Reflection/dynamic hotspots:** event discovery, entity mapping, and projection dispatch all use reflection; `((dynamic)this).Apply((dynamic)@event)` throws `RuntimeBinderException` on an unknown overload — mitigated only by the no-op fallback.
- **Sample bugs:** `LargeStreamModule` has dead code (`if (true)`), typos throughout (`Desciption`, `WriteBatchMadule`, `quatity`, `tableTransactionAcations`).
- **Stale .vscode:** `launch.json`/`tasks.json` reference `ConsoleApp1` from the original template.
- **README is thin** — no API docs, no package usage example.
- **No XML docs** on the public library API (important for a NuGet package).
- **Git hygiene:** commits are terse ("x", "y"); no tags/releases on GitHub despite a NuGet publish (the new release workflows create tags/releases going forward).

## Getting Work Done Here

- Verify any change against the sample (needs Azurite running) and add a test project before/while fixing library behavior.
- Prefer fixing `FluxStore` concurrency/ordering before building new features on top.
- Keep the public API simple — the project's whole premise is "simple event sourcing on Azure Tables."
- If migrating: multi-target `net8.0;net10.0` (or drop to `net10.0`), bump `Azure.Data.Tables` to 12.11.0, and update `dotnet-version` in all three workflows.
- Never change package versioning inline in the csproj — publish through the workflows (RELEASING.md).
