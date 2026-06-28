using System.Text.Json;
using SheraBoard.Core.Security;

namespace SheraBoard.Core.Persistence;

public sealed class FilePayloadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SheraBoardPaths _paths;
    private readonly IDataProtector _protector;

    public FilePayloadStore(SheraBoardPaths paths, IDataProtector protector)
    {
        _paths = paths;
        _protector = protector;
    }

    public async Task<string> WriteAsync<T>(Guid itemId, T payload, CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        var payloadRef = $"{itemId:N}-{typeof(T).Name}.payload";
        var path = ResolvePayloadPath(payloadRef);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var protectedBytes = _protector.Protect(jsonBytes);

        await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken);
        return payloadRef;
    }

    public async Task<T> ReadAsync<T>(string payloadRef, CancellationToken cancellationToken = default)
    {
        var protectedBytes = await File.ReadAllBytesAsync(ResolvePayloadPath(payloadRef), cancellationToken);
        var jsonBytes = _protector.Unprotect(protectedBytes);
        var payload = JsonSerializer.Deserialize<T>(jsonBytes, JsonOptions);
        return payload ?? throw new InvalidDataException($"Payload '{payloadRef}' could not be deserialized as {typeof(T).Name}.");
    }

    public string ResolvePayloadPath(string payloadRef)
    {
        var fileName = Path.GetFileName(payloadRef);
        return Path.Combine(_paths.PayloadsDirectory, fileName);
    }

    public long GetPayloadSize(string payloadRef)
    {
        var path = ResolvePayloadPath(payloadRef);
        return File.Exists(path) ? new FileInfo(path).Length : 0L;
    }

    public Task DeleteAsync(string payloadRef)
    {
        var path = ResolvePayloadPath(payloadRef);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}

