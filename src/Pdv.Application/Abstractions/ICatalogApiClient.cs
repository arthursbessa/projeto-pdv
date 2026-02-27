using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICatalogApiClient
{
    Task<IReadOnlyCollection<ProductCacheItem>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
