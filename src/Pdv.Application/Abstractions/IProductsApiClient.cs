using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IProductsApiClient
{
    Task<ProductAdminItem> CreateAsync(ProductAdminItem product, CancellationToken cancellationToken = default);
    Task<ProductAdminItem> UpdateAsync(ProductAdminItem product, CancellationToken cancellationToken = default);
    Task UpdatePriceAsync(string productId, int priceCents, CancellationToken cancellationToken = default);
}
