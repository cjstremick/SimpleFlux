using SimpleFlux.InMemory;

namespace SimpleFlux.Tests.Contract;

public class InMemoryContractTests : StreamStoreContractTests
{
    protected override IStreamStore CreateStore() => new InMemoryStreamStore();
}
