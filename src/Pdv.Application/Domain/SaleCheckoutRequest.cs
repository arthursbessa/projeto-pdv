namespace Pdv.Application.Domain;

public sealed class SaleCheckoutRequest
{
    public PaymentMethod PaymentMethod { get; init; }
    public int? ReceivedAmountCents { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public decimal DiscountPercent { get; init; }
    public bool ReceiptRequested { get; init; }
    public string? ReceiptTaxId { get; init; }
}
