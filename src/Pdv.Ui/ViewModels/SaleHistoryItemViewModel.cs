using Pdv.Application.Domain;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class SaleHistoryItemViewModel
{
    public required Guid SaleId { get; init; }
    public required string SaleIdentifier { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required PaymentMethod PaymentMethod { get; init; }
    public required string CustomerName { get; init; }
    public required string CashierName { get; init; }
    public required string ProductsSummary { get; init; }
    public required string Status { get; init; }
    public required int TotalCents { get; init; }
    public int? ReceivedAmountCents { get; init; }
    public int ChangeAmountCents { get; init; }

    public string DateText => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string PaymentText => PaymentMethod switch
    {
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.Card => "Cartao",
        PaymentMethod.Pix => "PIX",
        _ => PaymentMethod.ToString()
    };
    public string TotalFormatted => MoneyFormatter.FormatFromCents(TotalCents);
    public string StatusText => Status switch
    {
        "REFUNDED" => "Devolvida",
        "PARTIALLY_REFUNDED" => "Devolucao parcial",
        _ => "Concluida"
    };
}
