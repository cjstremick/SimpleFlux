namespace SimpleFlux;

/// <summary>
/// Marks a class as an event type and gives it a stable name for storage.
/// </summary>
/// <remarks>
/// The <see cref="Name"/> is what gets persisted in the <c>EventType</c> column and
/// is used to reconstruct the correct CLR type when events are read back. If a
/// <see cref="FluxEvent"/> subclass is not decorated, its CLR type name is used
/// instead. Event types must live in an assembly that is loaded when the
/// <see cref="FluxStore"/> is constructed.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class FluxEventAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute with the given event name.
    /// </summary>
    /// <param name="name">The stable name stored with the event.</param>
    public FluxEventAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The stable name stored with the event.
    /// </summary>
    public string Name { get; }
}