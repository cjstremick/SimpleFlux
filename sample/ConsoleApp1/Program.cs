using System.Diagnostics;
using Azure.Data.Tables;
using SimpleFlux;
using SimpleFlux.Sample.Modules;

var tableClient = new TableClient("UseDevelopmentStorage=true", "FluxStore");
tableClient.CreateIfNotExists();

var modules = new[]
{
    new {Number = "1", Module = new WriteSingleModule(tableClient) as SampleModule},
    new {Number = "2", Module = new WriteBatchModule(tableClient) as SampleModule},
    new {Number = "3", Module = new WriteHybridBatchMadule(tableClient) as SampleModule},
    new {Number = "4", Module = new ProjectionModule(tableClient) as SampleModule}
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

var c = new FluxStore(tableClient);
// await c.AddEvent(new ItemAdded("Cj", "1"));
// await c.AddEvent(new ItemAdded("Cj", "2"));

// await c.AddEvents(new FluxEvent[] {
//     new ItemAdded("Cj", "3"),
//     new ItemAdded("Cj", "1"),
//     new ItemChanged("Cj", "2", Faker.RandomNumber.Next(1, 10)),
//     new ItemRemoved("Cj", "3")
// });

// var sw = Stopwatch.StartNew();
// for (int i = 0; i < 100; i++)
// {
//     await c.AddEvent(new ItemAdded(Faker.Desciption.FullName(), Faker.RandomNumber.Next(1, 10).ToString()));
// }
// Console.WriteLine($"AddEvent: {sw.ElapsedMilliseconds}ms");

// sw.Restart();
// var events = new List<FluxEvent>();
// var names = Enumerable
//     .Range(1, 10)
//     .Select(_ => Faker.Desciption.FullName());
// foreach (var name in names)
// {
//     events.AddRange(Enumerable
//         .Range(1, 10)
//         .Select(_ => new ItemAdded(name, Faker.RandomNumber.Next(1, 10).ToString())));
// }
// await c.AddEvents(events);
// Console.WriteLine($"AddEvents: {sw.ElapsedMilliseconds}ms");

//var xx = c.GetEvents("Cj");
//foreach (var x in xx) Console.WriteLine(x);