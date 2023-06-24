using Azure.Data.Tables;
using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteSingleModule : SampleModule
{
    public WriteSingleModule(TableClient tableClient)
        : base(tableClient)
    {
    }

    public override string Desciption => "Create 20 events one at a time.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        var eventTasks = Enumerable
            .Range(0, 20)
            .Select(_ => new ItemAdded(sku, RandomNumber.Next(1, 100)))
            .Select(FluxStore.AddEvent);

        // FluxStore.AddEvent adds a single event to the table.  If sending more than one
        // event, consider using FluxStore.AddEvents instead.
        await Task.WhenAll(eventTasks);
    }
}