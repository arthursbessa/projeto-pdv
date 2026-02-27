namespace Pdv.Application.Domain;

public sealed class SaleSummary
{
    public string SaleId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public int TotalCents { get; init; }
}
