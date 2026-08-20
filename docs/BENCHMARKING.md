# SimpleFlux Benchmarking Design

Status: planned (tracked on branch `bench/benchmark-suite`).
Purpose: give SimpleFlux **measurable** performance and safety guarantees ahead of the 2.0.0 release, and a repeatable way to catch regressions (batch cliff, concurrency, reflection cost).

> Runtime note: the **In-Memory** store (`SimpleFlux.InMemory`) makes the full suite runnable in CI with **no Azure emulator and no network**. The **Azure Tables** store is parameterized alongside it to validate real-storage behavior locally (Azurite) and is the source of truth for anything storage-bound.

## Harness

- **`benchmarks/SimpleFlux.Benchmarks`** — a `net10.0` class library referencing `SimpleFlux`, `SimpleFlux.AzureTables`, `SimpleFlux.InMemory`, and `BenchmarkDotNet` (version pinned from `Directory.Packages.props`).
- Run with `dotnet run -c Release` from the project dir. Results land in `benchmarks/SimpleFlux.Benchmarks/artifacts/` (BenchmarkDotNet default).
- Parameterization: `[Params]` over backends (`InMemory`, `AzureTables`) so each scenario runs against both. Backend setup uses a fresh `FluxStore` per invocation with unique stream ids to avoid cross-test contamination.

## Scenarios

These map directly to real hotspots documented in `AGENTS.md` and the existing sample.

### Performance

| # | Name | What it measures | Why it matters |
|---|---|---|---|
| P1 | `Append_SingleEvent` | Latency of one `AddEvent` | Baseline append cost. |
| P2 | `Append_Batch` | Throughput of `AddEvents` vs batch size (1, 10, 25, 50, 75, 100, 125) | Finds the **100-entity Azure transaction cliff** — the known "no chunking" limit. 125 should degrade/fail on AzureTables. |
| P3 | `Read_Stream` | Time to read a stream of N events (180) | Read path cost incl. property mapping. |
| P4 | `Project_Stream` | Time to `ProjectTo<T>` over a 180-event stream | Replays the `LargeStreamModule` case; dominated by reflection dispatch (`(dynamic)Apply`). |
| P5 | `Project_Concurrency` | N concurrent writers to **one** stream (expect `FluxConcurrencyException` rate) and N concurrent projectors on disjoint streams | Contention + retry behavior. |
| P6 | `Reflection_Overhead` | Event discovery (`DiscoverEventTypes` once per `FluxStore`) + hydration per event | Quantifies the `GetProperties`/reflection tax; gives a baseline vs a hypothetical non-reflective dispatch. |

### Safety (behavioral assertions, not microbenchmarks)

These are correctness guards. Each is a `[Benchmark]` that asserts and throws on failure (BenchmarkDotNet surfaces failures; CI treats a non-zero exit as red).

| # | Name | Asserts |
|---|---|---|
| S1 | `Concurrency_ThrowsWithActualVersion` | Two concurrent appends expecting the same `expectedVersion` → the loser throws `FluxConcurrencyException` whose `ActualVersion` is the winner's new version (not stale). |
| S2 | `ReadOrder_MatchesAppendOrder` | After appending N events with increasing quantity values, `ReadStreamAsync` + `Hydrate` returns them in **Version order** (not timestamp). |
| S3 | `Projection_IgnoresUnknownEvents` | An `Apply(FluxEvent)` projection receiving events with no specific `Apply` overload does **not throw** `RuntimeBinderException` — the no-op fallback holds. |
| S4 | `Version_RestoredOnRead` | Each rehydrated event carries the `Version` it was written with (regression guard for the old "Version stays 0" bug). |

## Metrics to track over time

- Mean/min latency for P1, P3, P4.
- Throughput (events/sec) for P2, with a note at the 100-entity inflection.
- `FluxConcurrencyException` rate for P5.
- Reflection cost (ms) for P6.

The `LargeStreamModule` sample (180 events → project) is the **regression baseline**: if P4 regresses by >20% on InMemory, flag it.

## CI integration (future)

Once `BenchmarkDotNet` is added, wire a gated `benchmark.yml` workflow that:
- Runs against `InMemory` on every PR (fast lane).
- Runs against `AzureTables`/`Azurite` on `main` only (slow lane), comparing to the last successful run for drift.

## Conventions

- Mirror the repo's CPM setup: add `BenchmarkDotNet` to `Directory.Packages.props` (do **not** version it inline in the csproj).
- Use the same `Directory.Build.props` shared metadata.
- Fresh store + unique stream id per benchmark iteration (`Guid`-suffixed stream id) — `FluxStore` is stateless but backends hold table state; isolation matters especially for AzureTables.
- Prefer `InMemory` in `Main()` smoke runs; the parameterized backends handle the matrix.
