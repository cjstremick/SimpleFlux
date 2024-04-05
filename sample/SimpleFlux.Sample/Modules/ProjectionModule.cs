using Azure.Data.Tables;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class ProjectionModule : SampleModule
{
    public ProjectionModule(TableClient tableClient)
        : base(tableClient)
    {
    }

    public override string Desciption => "Create a projection from events.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        await Task.WhenAll(
            FluxStore.AddEvent(new ItemAdded(sku, 10)),
            FluxStore.AddEvent(new ItemRemoved(sku, 3)),
            FluxStore.AddEvent(new ItemAdded(sku, 6)),
            FluxStore.AddEvent(new ItemAdded(sku, 10)),
            FluxStore.AddEvent(new ItemRemoved(sku, 11))
        );

        var projection = await FluxStore.ProjectTo<ItemInventoryProjection>(sku);
        Console.WriteLine($"\r\n\tItem {sku} has {projection.Quantity} items in inventory.");
    }
}