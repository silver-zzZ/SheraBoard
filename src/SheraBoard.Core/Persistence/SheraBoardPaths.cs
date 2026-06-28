namespace SheraBoard.Core.Persistence;

public sealed record SheraBoardPaths(
    string RootDirectory,
    string DatabasePath,
    string SettingsPath,
    string PayloadsDirectory,
    string FileSnapshotsDirectory)
{
    public static SheraBoardPaths ForPortableRoot(string rootDirectory)
    {
        return new SheraBoardPaths(
            rootDirectory,
            Path.Combine(rootDirectory, "SheraBoard.sqlite3"),
            Path.Combine(rootDirectory, "settings.json"),
            Path.Combine(rootDirectory, "payloads"),
            Path.Combine(rootDirectory, "file-snapshots"));
    }

    public static SheraBoardPaths ForCurrentUser()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SheraBoard");
        return ForPortableRoot(root);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PayloadsDirectory);
        Directory.CreateDirectory(FileSnapshotsDirectory);
    }
}

