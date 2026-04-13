namespace Pdv.Application.Domain;

public sealed class SaleItem
{
    public string? SaleItemId { get; init; }
    public required string ProductId { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public int PriceCents { get; set; }
    public int RefundedQuantity { get; init; }
    public int Quantity { get; private set; } = 1;

    public int SubtotalCents => PriceCents * Quantity;
    public decimal Price => PriceCents / 100m;
    public decimal Subtotal => SubtotalCents / 100m;
    public int RemainingRefundQuantity => Math.Max(Quantity - RefundedQuantity, 0);

    public void IncrementQuantity() => Quantity++;

    public void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantidade deve ser maior que zero.");
        }

        Quantity = quantity;
    }

    public void SetPrice(int priceCents)
    {
        if (priceCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceCents), "Preco deve ser zero ou maior.");
        }

        PriceCents = priceCents;
    }
}
