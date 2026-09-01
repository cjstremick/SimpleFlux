namespace SimpleFlux.Sample.Modules;

public abstract class SampleModule
{
    protected readonly FluxStore FluxStore;

    protected SampleModule(FluxStore fluxStore)
    {
        FluxStore = fluxStore;
    }

    public abstract string Desciption { get; }
    public abstract Task Run();
}