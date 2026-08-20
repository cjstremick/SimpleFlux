using BenchmarkDotNet.Running;

// Entry point for `dotnet run -c Release --project benchmarks/SimpleFlux.Benchmarks`.
// Uses the default BenchmarkDotNet config (no custom toolchain needed for these scenarios).
var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
switcher.Run(args);
