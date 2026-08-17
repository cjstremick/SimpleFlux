using Azure;
using Azure.Data.Tables;

namespace SimpleFlux.AzureTables;

/// <summary>
/// The stream header entity in Azure Table Storage.
/// </summary>
/// <remarks>
/// Each stream has exactly one header row (RowKey = <see cref="FluxHeaderKey"/>) that
/// tracks the stream's current <see cref="Version"/>. It is updated transactionally
/// whenever events are appended, and it is excluded from event reads. The entity's
/// <see cref="ETag"/> backs optimistic concurrency for appends.
/// </remarks>
public class FluxHeader : ITableEntity
{
    /// <summary>
    /// The row key used for the header row of every stream.
    /// </summary>
    public const string FluxHeaderKey = "F-HEAD";

    /// <summary>
    /// Creates an empty header (used when a stream has no header yet).
    /// </summary>
    public FluxHeader()
    {
        RowKey = FluxHeaderKey;
    }

    /// <summary>
    /// Creates a header for the given stream id.
    /// </summary>
    /// <param name="id">The stream id (partition key).</param>
    public FluxHeader(string id)
        : this()
    {
        PartitionKey = id;
    }

    /// <summary>
    /// The current version (highest event version) of the stream.
    /// </summary>
    public int Version { get; set; }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string RowKey { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }
}