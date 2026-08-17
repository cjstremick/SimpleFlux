using System.Collections.Concurrent;

namespace SimpleFlux.InMemory;

/// <summary>
/// An <see cref="IStreamStore"/> implementation that keeps all streams in memory.
/// </summary>
/// <remarks>
/// Thread-safe (per-stream locking) with the same atomicity and optimistic-concurrency
/// semantics as durable backends. Data is lost when the process exits — use this for
/// quickstarts, demos, offline development, and tests.
/// </remarks>
public sealed class InMemoryStreamStore : IStreamStore
{
    private readonly ConcurrentDictionary<string, StreamData> _streams = new();

    /// <inheritdoc />
    public Task AppendToStreamAsync(string streamId, int expectedVersion, IReadOnlyList<FluxEventRecord> events, int newVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = _streams.GetOrAdd(streamId, static _ => new StreamData());

        lock (data.SyncRoot)
        {
            EnforceExpectedVersion(streamId, expectedVersion, data);

            data.Records.AddRange(events.OrderBy(e => e.Version));
            data.Version = newVersion;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_streams.TryGetValue(streamId, out var data))
        {
            return Task.FromResult<IReadOnlyList<FluxEventRecord>>(Array.Empty<FluxEventRecord>());
        }

        lock (data.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<FluxEventRecord>>(
                data.Records.OrderBy(e => e.Version).ToArray());
        }
    }

    /// <inheritdoc />
    public Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_streams.TryGetValue(streamId, out var data))
        {
            return Task.FromResult<FluxStreamMetadata?>(null);
        }

        lock (data.SyncRoot)
        {
            return Task.FromResult<FluxStreamMetadata?>(
                data.Version < 0
                    ? null
                    : new FluxStreamMetadata { StreamId = streamId, Version = data.Version });
        }
    }

    private static void EnforceExpectedVersion(string streamId, int expectedVersion, StreamData data)
    {
        var actualVersion = data.Version;
        var conflict = expectedVersion switch
        {
            -1 when actualVersion >= 0 => true,
            -2 => false,
            _ when actualVersion != expectedVersion => true,
            _ => false
        };
        if (conflict)
        {
            throw new FluxConcurrencyException(streamId, expectedVersion, actualVersion);
        }
    }

    private sealed class StreamData
    {
        public object SyncRoot { get; } = new();

        /// <summary>The current stream version; -1 when the stream has no events.</summary>
        public int Version { get; set; } = -1;

        public List<FluxEventRecord> Records { get; } = new();
    }
}