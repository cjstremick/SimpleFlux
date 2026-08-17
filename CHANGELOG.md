# Changelog

All notable changes to SimpleFlux are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
versioning follows [SemVer](https://semver.org). The release process lives in
[RELEASING.md](RELEASING.md).

## [Unreleased]

### Changed

- Migrated to **.NET 10** (LTS) — library and sample now target `net10.0`
- `Azure.Data.Tables` **12.8.3 → 12.11.0**
- New release pipeline: CI on every push/PR, plus manual prerelease and
  promote-to-stable publishing workflows (see RELEASING.md)
- NuGet package now ships the README and license (MIT) metadata
- Actions bumped to `actions/checkout@v7` / `actions/setup-dotnet@v6`

### Fixed

- Sample: `ProjectionModule` no longer dereferences a null projection on empty streams

## [1.0.0] - 2024-09-23

### Added

- Initial release: event sourcing on Azure Table Storage
  - `FluxStore` — add events, batch writes, projections, stream metadata
  - `FluxEvent` / `[FluxEvent]` / `[FluxProperty]` — event contracts
  - `FluxProjection` — replay-based projections with dynamic dispatch