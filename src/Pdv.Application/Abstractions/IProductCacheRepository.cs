using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IProductCacheRepository
{
    Task<ProductCacheItem?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task ReplaceCatalogAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default);
}
