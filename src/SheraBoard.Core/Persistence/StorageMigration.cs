namespace SheraBoard.Core.Persistence;

public static class StorageMigration
{
    public static Task<SheraBoardPaths> CopyToRootAsync(
        SheraBoardPaths oldPaths,
        string newRootDirectory,
        CancellationToken cancellationToken = default)
    {
        var oldRoot = NormalizeRoot(oldPaths.RootDirectory);
        var newRoot = NormalizeRoot(newRootDirectory);

        if (string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase))
        {
            oldPaths.EnsureDirectories();
            return Task.FromResult(oldPaths);
        }

        if (IsChildPath(newRoot, oldRoot))
        {
            throw new InvalidOperationException("New storage directory cannot be inside the current storage directory.");
        }

        var newPaths = SheraBoardPaths.ForPortableRoot(newRoot);
        newPaths.EnsureDirectories();

        CopyDatabaseFiles(oldPaths.DatabasePath, newPaths.DatabasePath);
        CopyFileIfExists(oldPaths.SettingsPath, newPaths.SettingsPath, overwrite: true);
        CopyDirectoryIfExists(oldPaths.PayloadsDirectory, newPaths.PayloadsDirectory, cancellationToken);
        CopyDirectoryIfExists(oldPaths.FileSnapshotsDirectory, newPaths.FileSnapshotsDirectory, cancellationToken);

        return Task.FromResult(newPaths);
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath, bool overwrite)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, targetPath, overwrite);
    }

    private static void CopyDatabaseFiles(string sourceDatabasePath, string targetDatabasePath)
    {
        CopyFileIfExists(sourceDatabasePath, targetDatabasePath, overwrite: true);
        CopyFileIfExists($"{sourceDatabasePath}-wal", $"{targetDatabasePath}-wal", overwrite: true);
        CopyFileIfExists($"{sourceDatabasePath}-shm", $"{targetDatabasePath}-shm", overwrite: true);
    }

    private static void CopyDirectoryIfExists(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            CopyFileIfExists(file, Path.Combine(targetDirectory, relativePath), overwrite: true);
        }
    }

    private static string NormalizeRoot(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsChildPath(string candidatePath, string parentPath)
    {
        return candidatePath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
