using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace SimpleFlux.Benchmarks;

/// <summary>
/// BenchmarkDotNet configuration. The in-process (no-emit) toolchain gives the fastest
/// edit/run loop for local iteration. It cannot run the AzureTables backend (no Azurite
/// in-proc), but that case is parameterized in and runs via the normal toolchain when the
/// emulator is available.
/// </summary>
public class DefaultConfig : ManualConfig
{
    public static IConfig Instance = new DefaultConfig();

    private DefaultConfig()
    {
        AddJob(Job.Default
            .WithId("quick")
            .WithStrategy(RunStrategy.Throughput)
            .WithWarmupCount(1)
            .WithIterationCount(3)
            .WithToolchain(InProcessNoEmitToolchain.Instance));
        AddColumns(BenchmarkDotNet.Columns.Statistic.Mean,
                   BenchmarkDotNet.Columns.Statistic.StdDev,
                   BenchmarkDotNet.Columns.Statistic.Median);
        Add(BenchmarkDotNet.Configs.DefaultConfig.Instance);
    }
}
