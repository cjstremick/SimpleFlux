using SimpleFlux.FlatFile;

namespace SimpleFlux.Tests.Contract;

public class FlatFileContractTests : StreamStoreContractTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"simpleflux-tests-{Guid.NewGuid():N}");

    protected override IStreamStore CreateStore()
    {
        Directory.CreateDirectory(_tempDir);
        return new FlatFileStreamStore(_tempDir);
    }

    public new void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        base.Dispose();
    }
}
