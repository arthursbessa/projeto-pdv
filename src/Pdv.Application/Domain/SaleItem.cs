namespace Pdv.Application.Domain;

public sealed class SaleItem
{
    public required string ProductId { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; private set; } = 1;

    public decimal Subtotal => UnitPrice * Quantity;

    public void IncrementQuantity() => Quantity++;

    public void DecrementQuantity()
    {
        if (Quantity > 0)
        {
            Quantity--;
        }
    }
}
