namespace SimpleFlux;

public abstract class FluxEvent
{
    protected FluxEvent(string id)
    {
        Id = id;
    }

    public string Id { get; set; }
    public int Version { get; set; }
}