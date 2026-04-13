namespace Pdv.Application.Domain;

public sealed class ProductCacheItem
{
    public required string ProductId { get; init; }
    public string Sku { get; set; } = string.Empty;
    public required string Barcode { get; set; }
    public required string Description { get; set; }
    public int PriceCents { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
