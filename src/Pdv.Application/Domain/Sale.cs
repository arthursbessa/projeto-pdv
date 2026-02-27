namespace Pdv.Application.Domain;

public sealed class Sale
{
    public Guid SaleId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public PaymentMethod PaymentMethod { get; init; }
    public decimal? ReceivedAmount { get; init; }
    public IReadOnlyCollection<SaleItem> Items { get; init; } = [];
    public decimal Total => Items.Sum(x => x.Subtotal);
}
