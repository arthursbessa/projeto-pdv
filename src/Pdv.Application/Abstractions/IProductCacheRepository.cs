using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IProductCacheRepository
{
    Task<ProductCacheItem?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCacheItem>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task<ProductCacheItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(ProductCacheItem product, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductCacheItem product, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(string id, bool active, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task SeedIfEmptyAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default);
}
