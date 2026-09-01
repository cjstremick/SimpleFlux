namespace SimpleFlux;

/// <summary>
/// Thrown by an <see cref="IStreamStore"/> when an append's expected version does not
/// match the stream's current version — i.e. the stream was modified concurrently.
/// </summary>
public class FluxConcurrencyException : Exception
{
    /// <summary>
    /// Creates a concurrency exception with the given conflict details.
    /// </summary>
    /// <param name="streamId">The stream that conflicted.</param>
    /// <param name="expectedVersion">The version the caller expected (-1 = stream must not exist, -2 = any).</param>
    /// <param name="actualVersion">The stream's actual version (-1 when the stream does not exist).</param>
    public FluxConcurrencyException(string streamId, int expectedVersion, int actualVersion)
        : base(BuildMessage(streamId, expectedVersion, actualVersion))
    {
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// The stream that conflicted.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// The version the caller expected.
    /// </summary>
    public int ExpectedVersion { get; }

    /// <summary>
    /// The stream's actual version (-1 when the stream does not exist).
    /// </summary>
    public int ActualVersion { get; }

    private static string BuildMessage(string streamId, int expectedVersion, int actualVersion)
    {
        var actual = actualVersion < 0 ? "the stream does not exist" : $"version {actualVersion}";
        return $"Concurrency conflict on stream '{streamId}': expected version {expectedVersion} but {actual}.";
    }
}