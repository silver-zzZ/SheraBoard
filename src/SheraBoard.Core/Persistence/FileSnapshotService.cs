using SheraBoard.Core.Models;

namespace SheraBoard.Core.Persistence;

public sealed class FileSnapshotService
{
    private readonly SheraBoardPaths _paths;
    private readonly StorageRepository _repository;
    private readonly FilePayloadStore _payloadStore;

    public FileSnapshotService(SheraBoardPaths paths, StorageRepository repository, FilePayloadStore payloadStore)
    {
        _paths = paths;
        _repository = repository;
        _payloadStore = payloadStore;
    }

    public async Task<string> SaveSnapshotAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetAsync(itemId, cancellationToken)
            ?? throw new InvalidOperationException("Clipboard item was not found.");
        if (item.Kind != ClipboardKind.FileList)
        {
            throw new InvalidOperationException("Only file-list clipboard items can save file snapshots.");
        }

        var payload = await _payloadStore.ReadAsync<FileListPayload>(item.PayloadRef, cancellationToken);
        var snapshotRoot = Path.Combine(_paths.FileSnapshotsDirectory, itemId.ToString("N"));
        Directory.CreateDirectory(snapshotRoot);

        foreach (var entry in payload.Items.Where(entry => entry.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                var target = Path.Combine(snapshotRoot, entry.Name);
                CopyDirectory(entry.OriginalPath, target);
            }
            else if (File.Exists(entry.OriginalPath))
            {
                File.Copy(entry.OriginalPath, Path.Combine(snapshotRoot, entry.Name), overwrite: true);
            }
        }

        var updated = payload with { SnapshotRef = snapshotRoot };
        var payloadRef = await _payloadStore.WriteAsync(itemId, updated, cancellationToken);
        await _repository.UpdatePayloadRefAsync(itemId, payloadRef, item.SizeBytes + DirectorySize(snapshotRoot), cancellationToken);
        return snapshotRoot;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(childDirectory, Path.Combine(targetDirectory, Path.GetFileName(childDirectory)));
        }
    }

    private static long DirectorySize(string directory)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length)
            : 0L;
    }
}

