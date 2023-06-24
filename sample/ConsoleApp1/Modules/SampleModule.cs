using Azure.Data.Tables;

namespace SimpleFlux.Sample.Modules;

public abstract class SampleModule
{
    protected readonly FluxStore FluxStore;

    protected SampleModule(TableClient tableClient)
    {
        FluxStore = new FluxStore(tableClient);
    }

    public abstract string Desciption { get; }
    public abstract Task Run();
}