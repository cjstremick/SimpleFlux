using Azure.Data.Tables;
using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteHybridBatchMadule : SampleModule
{
    public WriteHybridBatchMadule(TableClient tableClient)
        : base(tableClient)
    {
    }

    public override string Desciption => "Write 20 events in two 10-event batches.";

    public override async Task Run()
    {
        var skus = new[]
        {
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString()
        };
        var events = Enumerable
            .Range(0, 20)
            .Select((i, _) => new ItemAdded(skus[i%skus.Length], RandomNumber.Next(1, 100)));

        // FluxStore.AddEvents uses TableTransactionAction internally, but this requires
        // that all records have the same partition key (the events id field).  If sending a
        // batch of events with the different ids, FluxStore.AddEvents will create smaller
        // batches based on id and send those separately.  This will be slower than sending
        // a single batch, but could be faster than sending events individually.
        await FluxStore.AddEvents(events);
    }
}