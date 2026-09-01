# Changelog

All notable changes to SimpleFlux are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
versioning follows [SemVer](https://semver.org). The release process lives in
[RELEASING.md](RELEASING.md).

## [Unreleased]

### Added

- **Storage backend abstraction (2.0.0)** — `SimpleFlux` core is now storage-agnostic:
  - `IStreamStore` contract, `FluxEventRecord`, `FluxStreamMetadata`, `FluxConcurrencyException`
  - New packages: **`SimpleFlux.AzureTables`** and **`SimpleFlux.InMemory`** (second reference backend)
  - DI registration: `AddSimpleFlux()` + `UseAzureTables(...)` / `UseInMemory()` (`FluxOptions`, fluent `IFluxBuilder`)
  - Fluent event registration: `WithEvent<T>()`, `WithEvents<T1,T2,...>()` (batch), `ScanAssemblyOf<TMarker>()` / `ScanAssembly(Assembly)` — all require explicit registration; no implicit assembly scan
  - Optimistic concurrency on appends (`expected version` semantics) — concurrent writes now throw `FluxConcurrencyException` instead of racing
  - Event `Version` is restored on read; reads are ordered by version (backends)
  - Header lookups only treat HTTP 404 as "stream does not exist"; cancellation propagates

### Changed

- **Breaking:** `FluxStore` now takes `IStreamStore` (+ optional `FluxOptions`) instead of `TableClient`
- **Breaking:** `FluxHeader` moved from the core package to `SimpleFlux.AzureTables` (Azure-specific)
- Sample uses DI with the in-memory backend by default (Azurite now optional)
- Packages share metadata via `Directory.Build.props` (license, repo, readme, icon, symbols)

### Fixed

- Sample: `ProjectionModule`/`WriteSingleModule` written to respect the concurrency contract (sequential/batched appends)
- Removed the old typo'd module file names (`WriteBatchMadule`, `WriteHybridBatchMadule`)

## [1.1.0-alpha.1] - 2026-08-17

### Added

- `CancellationToken` support across the entire API (optional, backwards compatible)
- NuGet package metadata for consumers: description, icon, README, MIT license, repository link, changelog link
- Debug symbols (`.snupkg`) + SourceLink so consumers can step into the library source
- Central Package Management (`Directory.Packages.props`)

### Changed

- Migrated to **.NET 10** (LTS) — library and sample now target `net10.0`
- `Azure.Data.Tables` **12.8.3 → 12.11.0**
- New release pipeline: CI on every push/PR, plus manual prerelease and
  promote-to-stable publishing workflows (see RELEASING.md)
- NuGet package now ships the README and license (MIT) metadata
- Actions bumped to `actions/checkout@v7` / `actions/setup-dotnet@v6`

### Fixed

- Sample: `ProjectionModule` no longer dereferences a null projection on empty streams
- Sample: method-group usage of `AddEvent` broken by optional parameters (now a lambda)

## [1.0.0] - 2024-09-23

### Added

- Initial release: event sourcing on Azure Table Storage
  - `FluxStore` — add events, batch writes, projections, stream metadata
  - `FluxEvent` / `[FluxEvent]` / `[FluxProperty]` — event contracts
  - `FluxProjection` — replay-based projections with dynamic dispatch