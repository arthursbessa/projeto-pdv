namespace Pdv.Application.Domain;

public sealed class Sale
{
    public Guid SaleId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public PaymentMethod PaymentMethod { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? OperatorId { get; init; }
    public string? OperatorName { get; init; }
    public int? ReceivedAmountCents { get; init; }
    public int ChangeAmountCents { get; init; }
    public string Status { get; init; } = "COMPLETED";
    public string? RemoteSaleId { get; init; }
    public int? SaleNumber { get; init; }
    public string? CashRegisterSessionId { get; init; }
    public IReadOnlyCollection<SaleItem> Items { get; init; } = [];
    public int TotalCents => Items.Sum(x => x.SubtotalCents);
}
