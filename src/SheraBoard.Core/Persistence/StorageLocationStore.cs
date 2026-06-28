namespace SheraBoard.Core.Persistence;

public sealed class StorageLocationStore
{
    private readonly string _defaultRootDirectory;
    private readonly string _locationPath;
    private readonly IReadOnlyList<string> _legacyLocationPaths;

    public StorageLocationStore(string defaultRootDirectory, string locationPath)
        : this(defaultRootDirectory, locationPath, [])
    {
    }

    public StorageLocationStore(
        string defaultRootDirectory,
        string locationPath,
        IEnumerable<string> legacyLocationPaths)
    {
        _defaultRootDirectory = Path.GetFullPath(defaultRootDirectory);
        _locationPath = Path.GetFullPath(locationPath);
        _legacyLocationPaths = legacyLocationPaths
            .Select(Path.GetFullPath)
            .Where(path => !string.Equals(path, _locationPath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static StorageLocationStore ForCurrentUser()
    {
        var bootstrapRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SheraBoard");

        var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        return new StorageLocationStore(
            bootstrapRoot,
            Path.Combine(bootstrapRoot, "storage-location.txt"),
            EnumerateLegacyLocationPaths(Environment.CurrentDirectory, executableDirectory));
    }

    public string ResolveRootDirectory()
    {
        if (TryReadRootDirectory(_locationPath) is { } configuredRoot)
        {
            return configuredRoot;
        }

        foreach (var legacyLocationPath in _legacyLocationPaths)
        {
            if (TryReadRootDirectory(legacyLocationPath) is not { } legacyRoot)
            {
                continue;
            }

            TryWriteRootDirectory(_locationPath, legacyRoot);
            return legacyRoot;
        }

        return _defaultRootDirectory;
    }

    public SheraBoardPaths ResolvePaths()
    {
        return SheraBoardPaths.ForPortableRoot(ResolveRootDirectory());
    }

    public async Task SaveRootDirectoryAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        var normalizedRoot = Path.GetFullPath(rootDirectory);
        await WriteRootDirectoryAsync(_locationPath, normalizedRoot, cancellationToken);

        foreach (var legacyLocationPath in _legacyLocationPaths)
        {
            if (!File.Exists(legacyLocationPath))
            {
                continue;
            }

            await WriteRootDirectoryAsync(legacyLocationPath, normalizedRoot, cancellationToken);
        }
    }

    private static string? TryReadRootDirectory(string locationPath)
    {
        try
        {
            if (!File.Exists(locationPath))
            {
                return null;
            }

            var root = File.ReadAllText(locationPath).Trim();
            return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void TryWriteRootDirectory(string locationPath, string rootDirectory)
    {
        try
        {
            var directory = Path.GetDirectoryName(locationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(locationPath, Path.GetFullPath(rootDirectory));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task WriteRootDirectoryAsync(
        string locationPath,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(locationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{locationPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, Path.GetFullPath(rootDirectory), cancellationToken);

        if (File.Exists(locationPath))
        {
            File.Delete(locationPath);
        }

        File.Move(tempPath, locationPath);
    }

    private static IEnumerable<string> EnumerateLegacyLocationPaths(params string[] rootDirectories)
    {
        foreach (var rootDirectory in rootDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var currentDirectory = new DirectoryInfo(Path.GetFullPath(rootDirectory));
            for (var depth = 0; currentDirectory is not null && depth < 6; depth++)
            {
                yield return Path.Combine(currentDirectory.FullName, "data", "storage-location.txt");
                currentDirectory = currentDirectory.Parent;
            }
        }
    }
}
