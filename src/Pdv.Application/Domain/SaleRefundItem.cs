namespace Pdv.Application.Domain;

public sealed class SaleRefundItem
{
    public required string SaleItemId { get; init; }
    public required string ProductId { get; init; }
    public required string Description { get; init; }
    public int Quantity { get; init; }
}
