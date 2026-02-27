using Pdv.Application.Abstractions;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCatalogApiClient : ICatalogApiClient
{
    public Task<IReadOnlyCollection<ProductCacheItem>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<ProductCacheItem>>([]);
    }
}
