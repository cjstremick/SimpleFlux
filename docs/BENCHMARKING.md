# SimpleFlux Benchmarking Design

Status: **implemented & run** on branch `bench/benchmark-suite` (see
[docs/BENCHMARKING_RESULTS.md](BENCHMARKING_RESULTS.md) for the last captured run).
Purpose: give SimpleFlux **measurable** performance and safety guarantees ahead of the 2.0.0 release, and a repeatable way to catch regressions (batch cliff, concurrency, reflection cost).

> Runtime note: the **In-Memory** store (`SimpleFlux.InMemory`) makes the full suite runnable in CI with **no Azure emulator and no network**. The **Azure Tables** store is parameterized alongside it to validate real-storage behavior locally (Azurite) and is the source of truth for anything storage-bound.

## How to build & run

Requires the **.NET 10 SDK**. On this dev Mac the SDK lives at `~/.dotnetsdk`, so export it on `PATH` first. Args meant for the **benchmark** go **after `--`** (anything before is `dotnet run` args). `-j short` = ShortRun (fast smoke); omit for the default longer job.

```bash
export PATH="$HOME/.dotnetsdk:$PATH"
export DOTNET_ROOT="$HOME/.dotnetsdk"
cd ~/Projects/SimpleFlux

# Build once (SDK on PATH). NOTE: `dotnet build` takes the project as a positional
# arg — the `--project` form is for `dotnet run`, not `build`.
dotnet build -c Release benchmarks/SimpleFlux.Benchmarks

# Full suite (perf + safety), ShortRun, no Azure needed for InMemory
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*'

# Perf only / safety only
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*Benchmarks*'
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks -- -j short --filter '*SafetyBenchmarks*'
```

The `AzureTables` parameter needs [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) on the devstore ports (blob 10000, queue 10001, table 10002):

```bash
docker run -d --name azurite-bench -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

Results and JSON land under `benchmarks/SimpleFlux.Benchmarks/artifacts/` (BenchmarkDotNet default). The full (non-`short`) job takes several minutes per backend.

## Harness

- **`benchmarks/SimpleFlux.Benchmarks`** — a `net10.0` class library (entry point in `Program.cs` via `BenchmarkSwitcher`) referencing `SimpleFlux`, `SimpleFlux.AzureTables`, `SimpleFlux.InMemory`, and `BenchmarkDotNet` (version pinned from `Directory.Packages.props`).
- Consumed via the build/run commands above.
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
