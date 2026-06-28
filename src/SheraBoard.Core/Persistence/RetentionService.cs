namespace SheraBoard.Core.Persistence;

public sealed class RetentionService
{
    private readonly StorageRepository _repository;
    private readonly FilePayloadStore _payloadStore;

    public RetentionService(StorageRepository repository, FilePayloadStore payloadStore)
    {
        _repository = repository;
        _payloadStore = payloadStore;
    }

    public async Task EnforceCapacityAsync(long maxStorageBytes, CancellationToken cancellationToken = default)
    {
        var items = (await _repository.ListAsync(new ClipboardQuery(Limit: 10_000), cancellationToken)).ToList();
        var total = items.Sum(item => item.SizeBytes + _payloadStore.GetPayloadSize(item.PayloadRef));
        if (total <= maxStorageBytes)
        {
            return;
        }

        foreach (var item in items.OrderBy(item => item.CapturedAt))
        {
            if (item.Pinned || item.Favorite)
            {
                continue;
            }

            await _payloadStore.DeleteAsync(item.PayloadRef);
            await _repository.DeleteAsync(item.Id, cancellationToken);
            total -= item.SizeBytes;

            if (total <= maxStorageBytes)
            {
                return;
            }
        }
    }
}
