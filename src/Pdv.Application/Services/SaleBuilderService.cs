using Pdv.Application.Abstractions;
using Pdv.Application.Domain;

namespace Pdv.Application.Services;

public sealed class SaleBuilderService
{
    private readonly IProductCacheRepository _productCacheRepository;

    public SaleBuilderService(IProductCacheRepository productCacheRepository)
    {
        _productCacheRepository = productCacheRepository;
    }

    public async Task<(bool Added, string? Error, IReadOnlyCollection<SaleItem> Items)> AddByBarcodeAsync(
        string barcode,
        IList<SaleItem> currentItems,
        CancellationToken cancellationToken = default)
    {
        var normalized = barcode.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (false, "Código inválido.", currentItems.ToArray());
        }

        var product = await _productCacheRepository.FindByBarcodeAsync(normalized, cancellationToken);
        if (product is null || !product.Active)
        {
            return (false, "Produto não encontrado no catálogo local.", currentItems.ToArray());
        }

        var existing = currentItems.FirstOrDefault(x => x.ProductId == product.ProductId);
        if (existing is not null)
        {
            existing.IncrementQuantity();
            return (true, null, currentItems.ToArray());
        }

        currentItems.Add(new SaleItem
        {
            ProductId = product.ProductId,
            Barcode = product.Barcode,
            Description = product.Description,
            PriceCents = product.PriceCents
        });

        return (true, null, currentItems.ToArray());
    }
}
