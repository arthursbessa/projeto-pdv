using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IStoreSettingsApiClient
{
    Task<StoreSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);
}
