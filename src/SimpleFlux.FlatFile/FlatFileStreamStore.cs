using System.Text;
using System.Text.Json;

namespace SimpleFlux.FlatFile;

/// <summary>
/// An <see cref="IStreamStore"/> implementation that persists streams as JSONL files
/// on the local filesystem.
/// </summary>
/// <remarks>
/// Storage layout:
/// <code>
/// {root}/
///   {streamId}/
///     events.jsonl   — one JSON object per line, append-only
///     meta.json      — { "streamId": "...", "version": 123 }
/// </code>
/// Each stream is isolated in its own directory with a per-stream file lock for
/// concurrency. There is no transaction-size limit — the file system IS the
/// append mechanism. Data is durable to disk but lost if the process crashes
/// between writing an event line and updating meta.json (the next read will
/// see a version mismatch and can rebuild from the event file).
/// </remarks>
public sealed class FlatFileStreamStore : IStreamStore
{
    private readonly string _rootDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Creates a store over the given root directory.
    /// </summary>
    /// <param name="rootDirectory">
    /// The root directory for all stream storage. Created if it does not exist.
    /// </param>
    public FlatFileStreamStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <inheritdoc />
    public Task<FluxStreamMetadata?> GetStreamMetadataAsync(string streamId, CancellationToken cancellationToken = default)
    {
        var metaPath = GetMetaPath(streamId);
        if (!File.Exists(metaPath))
            return Task.FromResult<FluxStreamMetadata?>(null);

        var json = File.ReadAllText(metaPath, Encoding.UTF8);
        var meta = JsonSerializer.Deserialize<StreamMeta>(json, JsonOptions);
        if (meta is null || meta.Version < 0)
            return Task.FromResult<FluxStreamMetadata?>(null);

        return Task.FromResult<FluxStreamMetadata?>(
            new FluxStreamMetadata { StreamId = streamId, Version = meta.Version });
    }

    /// <inheritdoc />
    public Task AppendToStreamAsync(string streamId, long expectedVersion, IReadOnlyList<FluxEventRecord> events, long newVersion, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0) return Task.CompletedTask;

        EnsureStreamDirectory(streamId);

        using var lockFile = AcquireLock(streamId);

        var meta = ReadMeta(streamId);
        var currentVersion = meta?.Version ?? -1;

        ValidateExpectedVersion(streamId, expectedVersion, currentVersion);

        // Append events as JSON lines
        var eventsPath = GetEventsPath(streamId);
        using (var stream = new FileStream(eventsPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            foreach (var record in events)
            {
                var line = JsonSerializer.Serialize(record, JsonOptions);
                writer.WriteLine(line);
            }
        }

        // Update metadata
        var newMeta = new StreamMeta { StreamId = streamId, Version = newVersion };
        var metaJson = JsonSerializer.Serialize(newMeta, JsonOptions);
        var metaPath = GetMetaPath(streamId);
        var tempPath = metaPath + ".tmp";
        File.WriteAllText(tempPath, metaJson, Encoding.UTF8);
        File.Move(tempPath, metaPath, overwrite: true);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FluxEventRecord>> ReadStreamAsync(string streamId, CancellationToken cancellationToken = default)
    {
        var eventsPath = GetEventsPath(streamId);
        if (!File.Exists(eventsPath))
            return Task.FromResult<IReadOnlyList<FluxEventRecord>>(Array.Empty<FluxEventRecord>());

        var records = new List<FluxEventRecord>();
        foreach (var line in File.ReadLines(eventsPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var record = JsonSerializer.Deserialize<FluxEventRecord>(line, JsonOptions);
            if (record is not null)
                records.Add(record);
        }

        return Task.FromResult<IReadOnlyList<FluxEventRecord>>(
            records.OrderBy(r => r.Version).ToArray());
    }

    private void EnsureStreamDirectory(string streamId)
    {
        var dir = GetStreamDirectory(streamId);
        Directory.CreateDirectory(dir);
    }

    private string GetStreamDirectory(string streamId) =>
        Path.Combine(_rootDirectory, streamId);

    private string GetEventsPath(string streamId) =>
        Path.Combine(GetStreamDirectory(streamId), "events.jsonl");

    private string GetMetaPath(string streamId) =>
        Path.Combine(GetStreamDirectory(streamId), "meta.json");

    private StreamMeta? ReadMeta(string streamId)
    {
        var metaPath = GetMetaPath(streamId);
        if (!File.Exists(metaPath)) return null;

        var json = File.ReadAllText(metaPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<StreamMeta>(json, JsonOptions);
    }

    private static void ValidateExpectedVersion(string streamId, long expectedVersion, long currentVersion)
    {
        var conflict = expectedVersion switch
        {
            -1 when currentVersion >= 0 => true,   // NoStream: stream must not exist
            -2 => false,                            // Any: always ok
            _ when currentVersion != expectedVersion => true,
            _ => false
        };

        if (conflict)
            throw new FluxConcurrencyException(streamId, expectedVersion, currentVersion);
    }

    /// <summary>
    /// Acquires an exclusive file lock for the given stream.
    /// The lock is held for the lifetime of the returned FileStream (dispose to release).
    /// </summary>
    private FileStream AcquireLock(string streamId)
    {
        var lockPath = Path.Combine(GetStreamDirectory(streamId), ".lock");
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class StreamMeta
    {
        public string StreamId { get; set; } = string.Empty;
        public long Version { get; set; } = -1;
    }
}
