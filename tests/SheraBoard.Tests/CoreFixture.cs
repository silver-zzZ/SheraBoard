using SheraBoard.Core.Capture;
using SheraBoard.Core.Persistence;
using SheraBoard.Core.Security;
using SheraBoard.Core.Settings;

namespace SheraBoard.Tests;

internal sealed class CoreFixture : IDisposable
{
    private CoreFixture(string root, SheraBoardPaths paths, StorageRepository repository, FilePayloadStore payloadStore)
    {
        Root = root;
        Paths = paths;
        Repository = repository;
        PayloadStore = payloadStore;
    }

    public string Root { get; }

    public SheraBoardPaths Paths { get; }

    public StorageRepository Repository { get; }

    public FilePayloadStore PayloadStore { get; }

    public static async Task<CoreFixture> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "SheraBoard.Tests", Guid.NewGuid().ToString("N"));
        var paths = SheraBoardPaths.ForPortableRoot(root);
        Directory.CreateDirectory(paths.RootDirectory);
        Directory.CreateDirectory(paths.PayloadsDirectory);

        var repository = new StorageRepository(paths.DatabasePath);
        await repository.InitializeAsync();
        var payloadStore = new FilePayloadStore(paths, new DpapiDataProtector("SheraBoard.Tests"));

        return new CoreFixture(root, paths, repository, payloadStore);
    }

    public CapturePipeline CreatePipeline(AppSettings? settings = null)
    {
        return new CapturePipeline(Repository, PayloadStore, settings ?? AppSettings.Default);
    }

    public void Dispose()
    {
        Repository.DisposeAsync().AsTask().GetAwaiter().GetResult();

        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Test cleanup should not mask the test result.
        }
    }
}
