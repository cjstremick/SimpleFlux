using Azure.Data.Tables;
using Faker;
using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample.Modules;

public class WriteBatchModule : SampleModule
{
    public WriteBatchModule(TableClient tableClient)
        : base(tableClient)
    {
    }

    public override string Desciption => "Write 20 events in one batch.";

    public override async Task Run()
    {
        var sku = Guid.NewGuid().ToString();
        var events = Enumerable
            .Range(0, 20)
            .Select(_ => new ItemAdded(sku, RandomNumber.Next(1, 100)));

        // FluxStore.AddEvents uses TableTransactionAction internally, but this requires
        // that all records have the same partition key (the events id field).  If sending a
        // batch of events with the same id, the insert will be very quick.
        await FluxStore.AddEvents(events);
    }
}