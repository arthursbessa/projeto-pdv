using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class FinalizeSaleWindow : Window, INotifyPropertyChanged
{
    private bool _isBusy;
    private string _receivedAmountInput = string.Empty;
    private string _cashValidationMessage = string.Empty;
    private int _totalCents;
    private bool _isCashSelected = true;

    public FinalizeSaleWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            if (Owner?.DataContext is MainViewModel vm)
            {
                _totalCents = vm.TotalCents;
                OnPropertyChanged(nameof(TotalFormatted));
                ReceivedAmountInput = (vm.TotalCents / 100m).ToString("F2");
            }
        };
    }

    public PaymentMethod? SelectedPaymentMethod { get; private set; }
    public bool ShouldPrintCoupon { get; private set; }
    public Sale? CompletedSale { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string ReceivedAmountInput
    {
        get => _receivedAmountInput;
        set
        {
            if (SetField(ref _receivedAmountInput, value))
            {
                UpdateCashPreview();
            }
        }
    }

    public string TotalFormatted => MoneyFormatter.FormatFromCents(_totalCents);

    public bool IsCashSelected
    {
        get => _isCashSelected;
        private set => SetField(ref _isCashSelected, value);
    }

    public string ChangeFormatted
    {
        get
        {
            if (!MoneyFormatter.TryParseToCents(ReceivedAmountInput, out var receivedAmountCents))
            {
                return MoneyFormatter.FormatFromCents(0);
            }

            var changeCents = Math.Max(receivedAmountCents - _totalCents, 0);
            return MoneyFormatter.FormatFromCents(changeCents);
        }
    }

    public string CashValidationMessage
    {
        get => _cashValidationMessage;
        private set => SetField(ref _cashValidationMessage, value);
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (Owner?.DataContext is not MainViewModel vm)
        {
            return;
        }

        SelectedPaymentMethod = CashOption.IsChecked == true
            ? PaymentMethod.Cash
            : CardOption.IsChecked == true
                ? PaymentMethod.Card
                : PaymentMethod.Pix;

        var receivedAmountCents = SelectedPaymentMethod == PaymentMethod.Cash
            ? ValidateCashAmount()
            : null;

        if (SelectedPaymentMethod == PaymentMethod.Cash && !receivedAmountCents.HasValue)
        {
            return;
        }

        ShouldPrintCoupon = PrintCouponOption.IsChecked == true;

        IsBusy = true;
        try
        {
            CompletedSale = await vm.FinalizeAsync(SelectedPaymentMethod.Value, receivedAmountCents);
            if (CompletedSale is null)
            {
                return;
            }

            DialogResult = true;
            Close();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private void PaymentOption_Checked(object sender, RoutedEventArgs e)
    {
        IsCashSelected = CashOption.IsChecked == true;
        CashValidationMessage = string.Empty;

        if (IsCashSelected)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ReceivedAmountTextBox.Focus();
                ReceivedAmountTextBox.SelectAll();
            });
        }
    }

    private void UpdateCashPreview()
    {
        OnPropertyChanged(nameof(ChangeFormatted));
        if (!IsCashSelected)
        {
            CashValidationMessage = string.Empty;
            return;
        }

        _ = ValidateCashAmount();
    }

    private int? ValidateCashAmount()
    {
        if (!MoneyFormatter.TryParseToCents(ReceivedAmountInput, out var receivedAmountCents))
        {
            CashValidationMessage = "Informe um valor valido em dinheiro.";
            return null;
        }

        if (receivedAmountCents < _totalCents)
        {
            CashValidationMessage = "O valor recebido deve cobrir o total da venda.";
            return null;
        }

        CashValidationMessage = string.Empty;
        return receivedAmountCents;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
