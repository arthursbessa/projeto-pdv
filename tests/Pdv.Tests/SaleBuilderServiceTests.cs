using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Services;
using Xunit;

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
            if (barcode != "789") return Task.FromResult<ProductCacheItem?>(null);

            return Task.FromResult<ProductCacheItem?>(new ProductCacheItem
            {
                ProductId = "p-1",
                Barcode = "789",
                Description = "Produto Teste",
                PriceCents = 1000,
                Active = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<IReadOnlyList<ProductCacheItem>> SearchAsync(string? query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCacheItem>>([]);
        public Task<ProductCacheItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ProductCacheItem?>(null);
        public Task AddAsync(ProductCacheItem product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(ProductCacheItem product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ToggleActiveAsync(string id, bool active, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task SeedIfEmptyAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
