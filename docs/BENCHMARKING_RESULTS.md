# SimpleFlux Benchmark Results

Captured: 2026-08-20, on branch `bench/benchmark-suite`, Apple M4 / .NET 10.0.400 / Azurite (Docker) for AzureTables.
Job: `ShortRun` (warmup 3, iteration 3, launch 1). Treat numbers as directional (low iteration count), not final perf.
Full BenchmarkDotNet log: `/tmp/sf_full2.log`; BDN artifacts under `benchmarks/SimpleFlux.Benchmarks/artifacts/`.

## Machines / backends
- **InMemory** — `SimpleFlux.InMemory` (`InMemoryStreamStore`), lock-per-stream, no network.
- **AzureTables** — `SimpleFlux.AzureTables` (`AzureTableStreamStore`) against local Azurite (ports 10000/10001/10002).

## Performance summary (Mean, ns → µs → ms)

| Method | Backend | BatchSize | Mean | Notes |
|---|---|---|---|---|
| Append_SingleEvent | InMemory | 1 | ~0.96 µs | sub-microsecond, no network |
| Append_SingleEvent | AzureTables | 1 | ~2.37 ms | dominated by HTTP round-trip to Azurite |
| Append_Batch | InMemory | 1 | ~1.15 µs | |
| Append_Batch | InMemory | 10 | ~9.0 µs | linear in batch |
| Append_Batch | InMemory | 25 | ~22 µs | |
| Append_Batch | InMemory | 50 | ~43 µs | |
| Append_Batch | InMemory | 75 | ~64 µs | |
| Append_Batch | InMemory | 100 | ~88 µs | |
| Append_Batch | InMemory | 125 | ~108 µs | |
| Append_Batch | AzureTables | 1 | ~2.34 ms | |
| Append_Batch | AzureTables | 25 | ~8.10 ms | grows with batch |
| Append_Batch | AzureTables | 50 | ~9.59 ms | |
| Append_Batch | AzureTables | 75 | ~11.93 ms | |
| Append_Batch | AzureTables | 100 | **NA (fail)** | ⚠ the 100-entity transaction cliff |
| Append_Batch | AzureTables | 125 | **NA (fail)** | ⚠ the 100-entity transaction cliff |
| Read_Stream* | InMemory | all | ~100 µs | flat — independent of batch size |
| Read_Stream* | AzureTables | all | ~9–20 ms | grows with batch (warm chunks = round-trips) |
| Project_Stream* | InMemory | all | ~100 µs | flat |
| Project_Stream* | AzureTables | all | ~9–20 ms | grows with batch |
| Concurrency_Contention | InMemory | all | ~50 µs | dominated by Task scheduling |
| Concurrency_Contention | AzureTables | all | ~17–28 ms | network + 8-way contention |
| Reflection_Overhead | InMemory | 1 | ~1.22 µs | ⚠ negligible — refleciton is NOT the hotspot |
| Reflection_Overhead | AzureTables | 1 | ~1.24 µs | |

\* `Read_Stream` and `Project_Stream` warm a 180-event stream first (chunked in 50s to stay under the 100-entity cliff), then measure the read/projection.

## Headline findings

1. **The 100-entity transaction cliff is reproduced and real.** `Append_Batch` on AzureTables returns **NA at BatchSize 100 and 125** (the append is a single Azure table transaction of events+header; Azure Tables caps a transaction at 100 entities; the 2.0.0 store does not chunk — see AGENTS.md "Unchunked batch limit"). This is the known gap, now evidenced.

2. **InMemory is ~2,500× faster than Azurite for appends, ~100× for reads/projections.** Expected (localhost socket vs. in-process lock); Azurite's per-request HTTP latency (~2ms/op) dominates. For benchmarks and CI, InMemory is the right gatekeeper — AzureTables is for validating real-storage behavior only.

3. **Read/Project on InMemory are flat at ~100 µs regardless of stream size (1–125 woken).** The reflection-based `dynamic` projection dispatch does not degrade with event count on the InMemory backend.

4. **Reflection/discovery overhead is ~1.2 µs — negligible.** The AGENTS.md "reflection is a perf hotspot" concern is *not* borne out for the hot paths; the hotspot is I/O (network) and lock/task scheduling, not reflection. (Discovery runs once per `FluxStore` construction; hydration cost per event is buried under the read/project numbers.)

## Safety benchmarks (all passed — regression guards for 2.0.0 fixes)

| Method | Backend | Mean | Asserted |
|---|---|---|---|
| Concurrency_ThrowsWithActualVersion | InMemory | ~4.7 µs | concurrent writers raise `FluxConcurrencyException` with the winner's version > 0 |
| Projection_IgnoresUnknownEvents | InMemory | ~4.8 µs | `Apply(FluxEvent)` fallback ignores unknown events (no `RuntimeBinderException`) |
| Version_RestoredOnRead | InMemory | ~3.7 µs | hydrated event carries its assigned `Version` |
| ReadOrder_MatchesAppendOrder | InMemory | ~90 µs | read order matches append (version) order |

> Note: S1 was moved to assert at the `IStreamStore` contract (two clients sharing a stale expected version) because `FluxStore.AddEvent` always resolves the current version and so can't self-conflict under cooperative async. To validate the AzureTables ETag path the same way, add an `AzureTables` param to `SafetyBenchmarks` (Azurite is already up).

## How to reproduce

```bash
export PATH="$HOME/.dotnetsdk:$PATH"; export DOTNET_ROOT="$HOME/.dotnsdk"
cd ~/Projects/SimpleFlux
# Azurite (already running for these results):
docker run -d --name azurite-bench -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks \
  -- -j short --filter '*'
```
