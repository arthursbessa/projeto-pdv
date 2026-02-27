namespace Pdv.Application.Domain;

public sealed class Sale
{
    public Guid SaleId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public PaymentMethod PaymentMethod { get; init; }
    public int? ReceivedAmountCents { get; init; }
    public IReadOnlyCollection<SaleItem> Items { get; init; } = [];
    public int TotalCents => Items.Sum(x => x.SubtotalCents);
}
