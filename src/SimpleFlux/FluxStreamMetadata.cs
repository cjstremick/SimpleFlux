namespace SimpleFlux;

/// <summary>
/// The current state of a stream, as tracked by the backend.
/// </summary>
public sealed class FluxStreamMetadata
{
    /// <summary>
    /// The stream id.
    /// </summary>
    public required string StreamId { get; init; }

    /// <summary>
    /// The current version of the stream — the version of the last appended event.
    /// </summary>
    public int Version { get; init; }
}