using System.Diagnostics;
using Azure.Data.Tables;
using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class LargeStreamModule : SampleModule
{
    public LargeStreamModule(TableClient tableClient)
        : base(tableClient)
    {
    }

    public override string Desciption => "Create a stream with hundreds of events and project them.";

    public override async Task Run()
    {
        var sku = "massive-batch";
        var sw = Stopwatch.StartNew();

        Console.WriteLine();

        var streamExists = await FluxStore.StreamExists(sku);
        if (true)
        {
            Console.WriteLine("The test stream does not exist.  Creating it, now..."); 
            var count = 180;
            Console.Write($"Creating {count} events for sku '{sku}'...");
            var events = Enumerable
                .Range(0, count)
                .Select(_ => new ItemAdded(sku, RandomNumber.Next(1, 10)));
            Console.WriteLine($"Done.  ({sw.ElapsedMilliseconds}ms)");

            Console.Write("Writing the events...");
            await FluxStore.AddEvents(events);
            Console.WriteLine("Done.");
        }

        Console.Write("Getting the stream version...");
        var streamVersion = await FluxStore.GetStreamVersion(sku);
        Console.WriteLine($"Done.  (Version {streamVersion})");

        sw.Restart();
        Console.Write("Projecting the stream...");
        var projection = await FluxStore.ProjectTo<ItemInventoryProjection>(sku);
        Console.WriteLine($"Done.  ({sw.ElapsedMilliseconds}ms)");
    }
}