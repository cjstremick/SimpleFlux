namespace SimpleFlux;

/// <summary>
/// Base class for all events stored in a SimpleFlux stream.
/// </summary>
/// <remarks>
/// The <see cref="Id"/> identifies the stream the event belongs to (it becomes the
/// partition key in Azure Table Storage); <see cref="Version"/> is assigned by the
/// store when the event is written and is 0 before that.
/// </remarks>
public abstract class FluxEvent
{
    /// <summary>
    /// Creates an event belonging to the stream with the given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The stream id. All events in a stream share this id.</param>
    protected FluxEvent(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The stream id this event belongs to.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The position of this event within its stream, assigned by the store on write.
    /// </summary>
    public long Version { get; set; }
}