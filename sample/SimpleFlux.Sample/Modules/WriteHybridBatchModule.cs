using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteHybridBatchModule : SampleModule
{
    public WriteHybridBatchModule(FluxStore fluxStore)
        : base(fluxStore)
    {
    }

    public override string Desciption => "Write 20 events in two stream batches.";

    public override async Task Run()
    {
        var skus = new[]
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString()
        };
        var events = Enumerable
            .Range(0, 20)
            .Select((i, _) => new ItemAdded(skus[i % skus.Length], RandomNumber.Next(1, 100)));

        // FluxStore.AddEvents groups events by stream id and appends each group as one
        // atomic backend operation, sending the groups concurrently.
        await FluxStore.AddEvents(events);
    }
}