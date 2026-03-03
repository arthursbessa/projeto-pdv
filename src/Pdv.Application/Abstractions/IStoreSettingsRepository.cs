using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IStoreSettingsRepository
{
    Task<StoreSettings?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(StoreSettings settings, CancellationToken cancellationToken = default);
}
