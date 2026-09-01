using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using SimpleFlux;
using SimpleFlux.AzureTables;
using SimpleFlux.FlatFile;
using SimpleFlux.InMemory;
using SimpleFlux.Sample.Events;
using SimpleFlux.Sample.Modules;

var services = new ServiceCollection();

// Pick a backend:
//   - InMemory: runs anywhere with zero setup (this is the default below)
//   - AzureTables: needs Azurite or a real Azure Storage account
//   - FlatFile: local filesystem (JSONL per stream, zero deps)
//
// Event registration (pick one):
//   .WithEvent<ItemAdded>()              — register a single type
//   .WithEvents<ItemAdded, ItemRemoved>() — register multiple types
//   .ScanAssemblyOf<ItemAdded>()          — register every FluxEvent in the assembly
services
    .AddSimpleFlux()
    .WithEvents<ItemAdded, ItemRemoved>()
    .UseInMemory();
// services
//     .AddSimpleFlux()
//     .ScanAssemblyOf<ItemAdded>()
//     .UseAzureTables(new TableClient("UseDevelopmentStorage=true", "FluxStore"));
// services
//     .AddSimpleFlux()
//     .ScanAssemblyOf<ItemAdded>()
//     .UseFlatFile(Path.Combine(Path.GetTempPath(), "simpleflux-sample"));

var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<FluxStore>();

var modules = new[]
{
    new {Number = "1", Module = new WriteSingleModule(store) as SampleModule},
    new {Number = "2", Module = new WriteBatchModule(store) as SampleModule},
    new {Number = "3", Module = new WriteHybridBatchModule(store) as SampleModule},
    new {Number = "4", Module = new ProjectionModule(store) as SampleModule},
    new {Number = "5", Module = new LargeStreamModule(store) as SampleModule}
};


var isDone = false;
while (!isDone)
{
    foreach (var module in modules) Console.WriteLine($"{module.Number}. {module.Module.Desciption}");
    Console.WriteLine("X. Exit");
    Console.Write("Enter your choice: ");
    var response = Console.ReadLine();
    Console.WriteLine();

    if (response?.ToLower() == "x")
    {
        isDone = true;
        continue;
    }

    var selection = modules.SingleOrDefault(e => e.Number == response);
    if (selection != null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.Write($"Running {selection.Number}...");
        await selection.Module.Run();
        Console.WriteLine($"Done.  Took {sw.ElapsedMilliseconds}ms.\r\n");
    }
    else
    {
        Console.WriteLine("Invalid choice");
    }
}
