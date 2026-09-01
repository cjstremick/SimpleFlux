using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteBatchModule : SampleModule
{
    public WriteBatchModule(FluxStore fluxStore)
        : base(fluxStore)
    {
    }

    public override string Desciption => "Write 20 events in one batch.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        var events = Enumerable
            .Range(0, 20)
            .Select(_ => new ItemAdded(sku, RandomNumber.Next(1, 100)));

        // FluxStore.AddEvents uses one atomic backend append per stream — with a single
        // stream id the whole batch lands as one transaction.
        await FluxStore.AddEvents(events);
    }
}