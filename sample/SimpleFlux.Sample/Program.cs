using System.Diagnostics;
using Azure.Data.Tables;
using SimpleFlux.Sample.Modules;

var tableClient = new TableClient("UseDevelopmentStorage=true", "FluxStore");
tableClient.CreateIfNotExists();

var modules = new[]
{
    new {Number = "1", Module = new WriteSingleModule(tableClient) as SampleModule},
    new {Number = "2", Module = new WriteBatchModule(tableClient) as SampleModule},
    new {Number = "3", Module = new WriteHybridBatchMadule(tableClient) as SampleModule},
    new {Number = "4", Module = new ProjectionModule(tableClient) as SampleModule},
    new {Number = "5", Module = new LargeStreamModule(tableClient) as SampleModule}
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
        var sw = Stopwatch.StartNew();
        Console.Write($"Running {selection.Number}...");
        await selection.Module.Run();
        Console.WriteLine($"Done.  Took {sw.ElapsedMilliseconds}ms.\r\n");
    }
    else
    {
        Console.WriteLine("Invalid choice");
    }
}