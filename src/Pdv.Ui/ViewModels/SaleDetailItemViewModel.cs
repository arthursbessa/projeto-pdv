using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class SaleDetailItemViewModel : INotifyPropertyChanged
{
    private string _refundQuantityInput = string.Empty;

    public required string SaleItemId { get; init; }
    public required string ProductId { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public required int Quantity { get; init; }
    public required int RefundedQuantity { get; init; }
    public required int RemainingQuantity { get; init; }
    public required int PriceCents { get; init; }
    public required int SubtotalCents { get; init; }

    public string RefundQuantityInput
    {
        get => _refundQuantityInput;
        set
        {
            if (_refundQuantityInput == value)
            {
                return;
            }

            _refundQuantityInput = value;
            OnPropertyChanged();
        }
    }

    public string PriceFormatted => MoneyFormatter.FormatFromCents(PriceCents);
    public string SubtotalFormatted => MoneyFormatter.FormatFromCents(SubtotalCents);
    public bool CanRefund => RemainingQuantity > 0;

    public bool TryGetRefundQuantity(out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(RefundQuantityInput))
        {
            return false;
        }

        return int.TryParse(RefundQuantityInput, out quantity);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
