using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Services;

namespace Pdv.Tests;

public sealed class SaleBuilderServiceTests
{
    [Fact]
    public async Task AddByBarcodeAsync_ShouldAddAndIncrementWhenRepeated()
    {
        var repository = new FakeProductRepository();
        var sut = new SaleBuilderService(repository);
        var items = new List<SaleItem>();

        var first = await sut.AddByBarcodeAsync("789", items);
        var second = await sut.AddByBarcodeAsync("789", items);

        Assert.True(first.Added);
        Assert.True(second.Added);
        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    private sealed class FakeProductRepository : IProductCacheRepository
    {
        public Task<ProductCacheItem?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            if (barcode != "789")
            {
                return Task.FromResult<ProductCacheItem?>(null);
            }

            return Task.FromResult<ProductCacheItem?>(new ProductCacheItem
            {
                ProductId = "p-1",
                Barcode = "789",
                Description = "Produto Teste",
                Price = 10,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        public Task ReplaceCatalogAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
