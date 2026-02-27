namespace Pdv.Application.Domain;

public sealed class SaleItem
{
    public required string ProductId { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public int PriceCents { get; init; }
    public int Quantity { get; private set; } = 1;

    public int SubtotalCents => PriceCents * Quantity;
    public decimal Price => PriceCents / 100m;
    public decimal Subtotal => SubtotalCents / 100m;

    public void IncrementQuantity() => Quantity++;
}
