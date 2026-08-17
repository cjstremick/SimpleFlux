using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class ProjectionModule : SampleModule
{
    public ProjectionModule(FluxStore fluxStore)
        : base(fluxStore)
    {
    }

    public override string Desciption => "Create a projection from events.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        var events = new FluxEvent[]
        {
            new ItemAdded(sku, 10),
            new ItemRemoved(sku, 3),
            new ItemAdded(sku, 6),
            new ItemAdded(sku, 10),
            new ItemRemoved(sku, 11)
        };

        // One atomic append for the whole batch (versions 1..5) — concurrent appends
        // to the same stream would raise FluxConcurrencyException instead.
        await FluxStore.AddEvents(events);

        var projection = await FluxStore.ProjectTo<ItemInventoryProjection>(sku);
        if (projection == null)
        {
            Console.WriteLine($"\r\n\tItem {sku} has no events yet.");
            return;
        }
        Console.WriteLine($"\r\n\tItem {sku} has {projection.Quantity} items in inventory.");
    }
}