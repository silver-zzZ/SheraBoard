using SheraBoard.Core.Persistence;

namespace SheraBoard.Tests;

public sealed class StorageLocationTests
{
    [Fact]
    public async Task StorageLocationStoreReturnsDefaultRootUntilOverrideIsSaved()
    {
        var bootstrapRoot = Path.Combine(Path.GetTempPath(), "SheraBoard.Bootstrap", Guid.NewGuid().ToString("N"));
        var defaultRoot = Path.Combine(bootstrapRoot, "default-data");
        var customRoot = Path.Combine(bootstrapRoot, "custom-data");
        var store = new StorageLocationStore(defaultRoot, Path.Combine(bootstrapRoot, "storage-location.txt"));

        Assert.Equal(defaultRoot, store.ResolveRootDirectory());

        await store.SaveRootDirectoryAsync(customRoot);

        Assert.Equal(customRoot, store.ResolveRootDirectory());
    }

    [Fact]
    public void StorageLocationStorePromotesLegacyOverrideToStablePointer()
    {
        var bootstrapRoot = Path.Combine(Path.GetTempPath(), "SheraBoard.Bootstrap", Guid.NewGuid().ToString("N"));
        var defaultRoot = Path.Combine(bootstrapRoot, "default-data");
        var customRoot = Path.Combine(bootstrapRoot, "custom-data");
        var stablePointer = Path.Combine(bootstrapRoot, "storage-location.txt");
        var legacyPointer = Path.Combine(bootstrapRoot, "legacy", "storage-location.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPointer)!);
        File.WriteAllText(legacyPointer, customRoot);

        var store = new StorageLocationStore(defaultRoot, stablePointer, [legacyPointer]);

        Assert.Equal(customRoot, store.ResolveRootDirectory());
        Assert.Equal(customRoot, File.ReadAllText(stablePointer));
    }

    [Fact]
    public async Task StorageLocationStoreUpdatesExistingLegacyPointerWhenSaving()
    {
        var bootstrapRoot = Path.Combine(Path.GetTempPath(), "SheraBoard.Bootstrap", Guid.NewGuid().ToString("N"));
        var defaultRoot = Path.Combine(bootstrapRoot, "default-data");
        var customRoot = Path.Combine(bootstrapRoot, "custom-data");
        var stablePointer = Path.Combine(bootstrapRoot, "storage-location.txt");
        var legacyPointer = Path.Combine(bootstrapRoot, "legacy", "storage-location.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPointer)!);
        File.WriteAllText(legacyPointer, defaultRoot);
        var store = new StorageLocationStore(defaultRoot, stablePointer, [legacyPointer]);

        await store.SaveRootDirectoryAsync(customRoot);

        Assert.Equal(customRoot, File.ReadAllText(stablePointer));
        Assert.Equal(customRoot, File.ReadAllText(legacyPointer));
    }

    [Fact]
    public async Task StorageMigrationCopiesExistingDataAndRejectsNestedTarget()
    {
        var oldRoot = Path.Combine(Path.GetTempPath(), "SheraBoard.Migration", Guid.NewGuid().ToString("N"), "old");
        var newRoot = Path.Combine(Path.GetTempPath(), "SheraBoard.Migration", Guid.NewGuid().ToString("N"), "new");
        var oldPaths = SheraBoardPaths.ForPortableRoot(oldRoot);
        oldPaths.EnsureDirectories();
        await File.WriteAllTextAsync(oldPaths.DatabasePath, "db");
        await File.WriteAllTextAsync($"{oldPaths.DatabasePath}-wal", "wal");
        await File.WriteAllTextAsync($"{oldPaths.DatabasePath}-shm", "shm");
        await File.WriteAllTextAsync(oldPaths.SettingsPath, "settings");
        await File.WriteAllTextAsync(Path.Combine(oldPaths.PayloadsDirectory, "payload.bin"), "payload");
        await File.WriteAllTextAsync(Path.Combine(oldPaths.FileSnapshotsDirectory, "snapshot.bin"), "snapshot");

        var newPaths = await StorageMigration.CopyToRootAsync(oldPaths, newRoot);

        Assert.Equal("db", await File.ReadAllTextAsync(newPaths.DatabasePath));
        Assert.Equal("wal", await File.ReadAllTextAsync($"{newPaths.DatabasePath}-wal"));
        Assert.Equal("shm", await File.ReadAllTextAsync($"{newPaths.DatabasePath}-shm"));
        Assert.Equal("settings", await File.ReadAllTextAsync(newPaths.SettingsPath));
        Assert.Equal("payload", await File.ReadAllTextAsync(Path.Combine(newPaths.PayloadsDirectory, "payload.bin")));
        Assert.Equal("snapshot", await File.ReadAllTextAsync(Path.Combine(newPaths.FileSnapshotsDirectory, "snapshot.bin")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StorageMigration.CopyToRootAsync(oldPaths, Path.Combine(oldRoot, "nested")));
    }
}
