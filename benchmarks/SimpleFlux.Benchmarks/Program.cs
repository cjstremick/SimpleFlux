using BenchmarkDotNet.Running;

// Entry point for `dotnet run -c Release` from the benchmarks project dir.
// Runs every [Benchmark] attributed method in this assembly.
BenchmarkRunner.AutoStart(typeof(Benchmarks).Assembly);
