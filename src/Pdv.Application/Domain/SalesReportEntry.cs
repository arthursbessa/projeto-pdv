namespace Pdv.Application.Domain;

public sealed class SalesReportEntry
{
    public string SaleId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string BusinessDate { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public int TotalCents { get; init; }
    public string OperatorName { get; init; } = string.Empty;
}
