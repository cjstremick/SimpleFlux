using Azure.Data.Tables;
using SimpleFlux.AzureTables;

namespace SimpleFlux.Tests.Contract;

/// <summary>
/// Azure Tables contract tests. Requires Azurite running on standard devstore ports.
/// Skip with: dotnet test --filter "FullyQualifiedName!~AzureTables"
/// </summary>
public class AzureTablesContractTests : StreamStoreContractTests
{
    private readonly TableClient _client;

    public AzureTablesContractTests()
    {
        var tableName = $"test{Guid.NewGuid():N}";
        _client = new TableClient("UseDevelopmentStorage=true", tableName);
        _client.CreateIfNotExists();
    }

    protected override IStreamStore CreateStore() => new AzureTableStreamStore(_client);
}
