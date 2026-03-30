namespace Pdv.Application.Domain;

public sealed class SaleHistoryEntry
{
    public Guid SaleId { get; init; }
    public string SaleIdentifier { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public int TotalCents { get; init; }
    public int? ReceivedAmountCents { get; init; }
    public int ChangeAmountCents { get; init; }
    public string CustomerName { get; init; } = "Consumidor final";
    public string CashierName { get; init; } = "-";
    public string ProductsSummary { get; init; } = string.Empty;
    public string Status { get; init; } = "COMPLETED";
}
