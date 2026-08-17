using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteSingleModule : SampleModule
{
    public WriteSingleModule(FluxStore fluxStore)
        : base(fluxStore)
    {
    }

    public override string Desciption => "Create 20 events one at a time.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        var eventTasks = Enumerable
            .Range(0, 20)
            .Select(_ => new ItemAdded(sku, RandomNumber.Next(1, 100)));

        // FluxStore.AddEvent appends one event at a time (sequential — concurrent
        // appends to the same stream raise FluxConcurrencyException). For a single
        // atomic append, use FluxStore.AddEvents instead.
        foreach (var @event in eventTasks) await FluxStore.AddEvent(@event);
    }
}