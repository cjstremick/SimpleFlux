using Azure;
using Azure.Data.Tables;

namespace SimpleFlux;

public class FluxHeader : ITableEntity
{
    public const string FluxHeaderKey = "F-HEAD";

    public FluxHeader()
    {
        RowKey = FluxHeaderKey;
    }

    public FluxHeader(string id)
        : this()
    {
        PartitionKey = id;
    }

    public int Version { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}