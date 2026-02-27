namespace Pdv.Application.Services;

public sealed class SyncService
{
    public Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
