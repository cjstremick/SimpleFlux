namespace SimpleFlux;

public abstract class FluxProjection
{
    protected FluxProjection(string id)
    {
        Id = id;
    }

    public string Id { get; private set; }

    public void Load(IEnumerable<FluxEvent> events)
    {
        foreach (var e in events) ApplyChange(e);
    }

    protected void ApplyChange(FluxEvent @event)
    {
        if (@event.Id != Id)
            throw new InvalidOperationException("All events must belong to the same stream.");

        Id = @event.Id;
        ((dynamic) this).Apply((dynamic) @event);
    }

#pragma warning disable CA1822 // Mark members as static
    public void Apply(FluxEvent _)
#pragma warning restore CA1822 // Mark members as static
    {
        // no-op
    }
}