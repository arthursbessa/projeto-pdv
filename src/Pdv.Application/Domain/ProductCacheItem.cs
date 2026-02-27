namespace Pdv.Application.Domain;

public sealed class ProductCacheItem
{
    public required string ProductId { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
