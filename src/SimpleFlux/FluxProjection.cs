namespace SimpleFlux;

/// <summary>
/// Base class for projections — read models rebuilt by replaying a stream's events.
/// </summary>
/// <remarks>
/// Subclass and implement a <c>public void Apply(SomeEvent e)</c> method per event
/// type you care about; <see cref="Load"/> routes each event to the matching method
/// via dynamic dispatch. Events without a matching <c>Apply</c> overload are ignored
/// by the built-in fallback.
/// </remarks>
public abstract class FluxProjection
{
    /// <summary>
    /// Creates a projection for the given stream id.
    /// </summary>
    /// <param name="id">The stream id to project.</param>
    protected FluxProjection(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The stream id this projection is built from.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// Replays the given events through this projection's <c>Apply</c> methods.
    /// </summary>
    /// <param name="events">The stream's events, in version order.</param>
    public void Load(IEnumerable<FluxEvent> events)
    {
        foreach (var e in events) ApplyChange(e);
    }

    /// <summary>
    /// Routes a single event to the matching <c>Apply</c> overload.
    /// </summary>
    /// <param name="event">The event to apply.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the event belongs to a different stream than this projection.
    /// </exception>
    protected void ApplyChange(FluxEvent @event)
    {
        if (@event.Id != Id)
            throw new InvalidOperationException("All events must belong to the same stream.");

        Id = @event.Id;
        ((dynamic) this).Apply((dynamic) @event);
    }

#pragma warning disable CA1822 // Mark members as static
    /// <summary>
    /// Fallback that ignores event types without a matching <c>Apply</c> overload.
    /// </summary>
    /// <param name="_">The unhandled event.</param>
    public void Apply(FluxEvent _)
#pragma warning restore CA1822 // Mark members as static
    {
        // no-op
    }
}