namespace SimpleFlux;

/// <summary>
/// The storage contract implemented by SimpleFlux backends (Azure Tables, InMemory, ...).
/// </summary>
/// <remarks>
/// The core <see cref="FluxStore"/> owns versioning and event mapping; a backend only
/// has to persist <see cref="FluxEventRecord"/>s and stream metadata atomically and
/// enforce the expected version semantics of <see cref="AppendToStreamAsync(string, int, IReadOnlyList{FluxEventRecord}, int, CancellationToken)"/>.
/// See docs/ARCHITECTURE.md for the full contract description.
/// </remarks>
public interface IStreamStore
{
    /// <summary>
    /// Appends a batch of records to a stream and advances the stream version, atomically.
    /// </summary>
    /// <remarks>
    /// <paramref name="expectedVersion"/> semantics:
    /// <c>-2</c> = any version (append unconditionally), <c>-1</c> = the stream must not
    /// exist yet, <c>&gt;= 0</c> = exact expected version. On violation the backend throws
    /// <see cref="FluxConcurrencyException"/> and writes nothing.
    /// </remarks>
    /// <param name="streamId">The stream to append to.</param>
    /// <param name="expectedVersion">The stream version the caller believes is current.</param>
    /// <param name="events">The records to append, with versions already assigned.</param>
    /// <param name="newVersion">The stream version after this append (the last event version).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <exception cref="FluxConcurrencyException">Thrown when the current version does not match <paramref name="expectedVersion"/>.</exception>
    Task AppendToStreamAsync(string streamId, int expectedVersion, IReadOnlyList<FluxEventRecord> events, int newVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all records of a stream, ordered by version.
    /// </summary>
    /// <param name="streamId">The stream to read.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The stream's records in version order (empty when the stream has none).</returns>
    Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current metadata of a stream.
    /// </summary>
    /// <param name="streamId">The stream to inspect.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>The stream metadata, or <c>null</c> when the stream does not exist.</returns>
    Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken cancellationToken = default);
}