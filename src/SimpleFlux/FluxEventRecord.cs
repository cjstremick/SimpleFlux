namespace SimpleFlux;

/// <summary>
/// A storage-neutral representation of one persisted event.
/// </summary>
/// <remarks>
/// Backends store and return records without knowing anything about the concrete
/// event types; <see cref="FluxStore"/> converts between <see cref="FluxEvent"/> and
/// <see cref="FluxEventRecord"/> using the event discovery and
/// <see cref="FluxPropertyAttribute"/> mapping.
/// </remarks>
public sealed class FluxEventRecord
{
    /// <summary>
    /// The stable event type name (the <see cref="FluxEventAttribute"/> name, or the
    /// CLR type name when no attribute is present).
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The event's position within its stream.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// The persisted property values, keyed by <see cref="FluxPropertyAttribute"/> name.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Properties { get; init; }
}