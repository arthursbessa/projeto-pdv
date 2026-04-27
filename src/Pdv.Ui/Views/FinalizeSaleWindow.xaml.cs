using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
using Pdv.Ui.Formatting;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class FinalizeSaleWindow : Window, INotifyPropertyChanged
{
    private bool _isBusy;
    private string _receivedAmountInput = string.Empty;
    private string _cashValidationMessage = string.Empty;
    private int _totalCents;
    private bool _isCashSelected;
    private string _discountPercentInput = "0";
    private string _selectedCustomerDisplay = "Nenhum cliente vinculado";
    private string? _selectedCustomerId;
    private string? _selectedCustomerName;

    public FinalizeSaleWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            if (Owner?.DataContext is MainViewModel vm)
            {
                _totalCents = vm.TotalCents;
                DiscountPercentInput = vm.DefaultDiscountPercent.ToString("0.##");
                OnPropertyChanged(nameof(SaleValueFormatted));
                OnPropertyChanged(nameof(DiscountAppliedFormatted));
                OnPropertyChanged(nameof(TotalWithDiscountFormatted));
                OnPropertyChanged(nameof(DiscountPreviewFormatted));
                OnPropertyChanged(nameof(DiscountButtonText));
                OnPropertyChanged(nameof(CustomerButtonText));
            }

            CashOption.IsChecked = false;
            PixOption.IsChecked = false;
            CreditCardOption.IsChecked = false;
            DebitCardOption.IsChecked = false;
            IsCashSelected = false;
            CashValidationMessage = string.Empty;
            ReceivedAmountInput = string.Empty;
        };
    }

    public Sale? CompletedSale { get; private set; }
    public string? PrintedTaxId { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
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

    public string DiscountPercentInput
    {
        get => _discountPercentInput;
        set
        {
            if (SetField(ref _discountPercentInput, value))
            {
                OnPropertyChanged(nameof(DiscountPreviewFormatted));
                OnPropertyChanged(nameof(DiscountAppliedFormatted));
                OnPropertyChanged(nameof(TotalWithDiscountFormatted));
                OnPropertyChanged(nameof(TotalWithDiscountCents));
                OnPropertyChanged(nameof(DiscountButtonText));
                if (IsCashSelected)
                {
                    ReceivedAmountInput = (TotalAfterPercentageDiscountCents / 100m).ToString("F2");
                }
                UpdateCashPreview();
            }
        }
    }

    public string SaleValueFormatted => MoneyFormatter.FormatFromCents(_totalCents);
    public string SubtotalFormatted => SaleValueFormatted;

    public int DiscountPreviewCents
    {
        get
        {
            if (!decimal.TryParse(
                    DiscountPercentInput.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var percent))
            {
                return 0;
            }

            percent = Math.Clamp(percent, 0m, 100m);
            return (int)Math.Round(_totalCents * (percent / 100m), MidpointRounding.AwayFromZero);
        }
    }

    private int TotalAfterPercentageDiscountCents => Math.Max(_totalCents - DiscountPreviewCents, 0);

    private int CashShortageDiscountCents
    {
        get
        {
            if (!IsCashSelected)
            {
                return 0;
            }

            if (!MoneyFormatter.TryParseToCents(ReceivedAmountInput, out var receivedAmountCents))
            {
                return 0;
            }

            return Math.Max(TotalAfterPercentageDiscountCents - receivedAmountCents, 0);
        }
    }

    public int TotalWithDiscountCents => Math.Max(TotalAfterPercentageDiscountCents - CashShortageDiscountCents, 0);

    public int DiscountAppliedCents => Math.Max(_totalCents - TotalWithDiscountCents, 0);

    public string DiscountPreviewFormatted => MoneyFormatter.FormatFromCents(DiscountPreviewCents);
    public string DiscountAppliedFormatted => MoneyFormatter.FormatFromCents(DiscountAppliedCents);
    public string TotalWithDiscountFormatted => MoneyFormatter.FormatFromCents(TotalWithDiscountCents);
    public bool HasDiscountApplied => DiscountAppliedCents > 0;

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

            var changeCents = Math.Max(receivedAmountCents - TotalWithDiscountCents, 0);
            return MoneyFormatter.FormatFromCents(changeCents);
        }
    }

    public string CashValidationMessage
    {
        get => _cashValidationMessage;
        private set => SetField(ref _cashValidationMessage, value);
    }

    public bool ShowBackButton => false;

    public string SelectedCustomerDisplay
    {
        get => _selectedCustomerDisplay;
        private set => SetField(ref _selectedCustomerDisplay, value);
    }

    public string DiscountButtonText => string.IsNullOrWhiteSpace(DiscountPercentInput)
        ? "Desconto"
        : $"Desconto {DiscountPercentInput.Trim()}%";
    public string CustomerButtonText => string.IsNullOrWhiteSpace(_selectedCustomerId) ? "Cliente" : "Alterar cliente";
    public string DiscountShortcutLabel => Owner?.DataContext is MainViewModel vm ? vm.ChangePriceShortcutLabel : "F5";
    public string CustomerShortcutLabel => Owner?.DataContext is MainViewModel vm ? vm.SelectCustomerShortcutLabel : "F7";

    private void PaymentOption_Checked(object sender, RoutedEventArgs e)
    {
        IsCashSelected = CashOption.IsChecked == true;
        CashValidationMessage = string.Empty;

        if (IsCashSelected)
        {
            ReceivedAmountInput = (TotalAfterPercentageDiscountCents / 100m).ToString("F2");
            Dispatcher.BeginInvoke(() =>
            {
                ReceivedAmountTextBox.Focus();
                ReceivedAmountTextBox.SelectAll();
            });
        }
        else if (!string.IsNullOrWhiteSpace(ReceivedAmountInput))
        {
            ReceivedAmountInput = string.Empty;
        }

        RefreshSalePreview();
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        await FinalizeAsync(forcePrintReceipt: false);
    }

    private async Task FinalizeAsync(bool forcePrintReceipt)
    {
        if (Owner?.DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!TryGetSelectedPaymentMethod(out var paymentMethod))
        {
            MessageBox.Show(this, "Selecione uma forma de pagamento.");
            return;
        }

        if (paymentMethod == PaymentMethod.Cash && !ValidateCashAmount().HasValue)
        {
            return;
        }

        if (!decimal.TryParse(
                DiscountPercentInput.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var discountPercent))
        {
            if (!string.IsNullOrWhiteSpace(DiscountPercentInput))
            {
                MessageBox.Show(this, "Informe um desconto percentual valido.");
                return;
            }

            discountPercent = 0m;
        }

        string? receiptTaxId = null;
        var receiptRequested = forcePrintReceipt;

        if (!forcePrintReceipt)
        {
            var receiptDecision = new ReceiptDecisionWindow
            {
                Owner = this
            };

            if (receiptDecision.ShowDialog() != true)
            {
                return;
            }

            receiptRequested = receiptDecision.PrintReceipt;
        }

        if (receiptRequested)
        {
            var receiptPrompt = new ReceiptPromptWindow
            {
                Owner = this
            };

            if (receiptPrompt.ShowDialog() != true)
            {
                return;
            }

            receiptTaxId = receiptPrompt.ReceiptTaxId;
        }

        var request = new SaleCheckoutRequest
        {
            PaymentMethod = paymentMethod,
            ReceivedAmountCents = paymentMethod == PaymentMethod.Cash ? ValidateCashAmount() : null,
            CustomerId = _selectedCustomerId,
            CustomerName = _selectedCustomerName,
            DiscountPercent = discountPercent,
            ReceiptRequested = receiptRequested,
            ReceiptTaxId = receiptTaxId
        };

        if (paymentMethod == PaymentMethod.Cash && !request.ReceivedAmountCents.HasValue)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CompletedSale = await vm.FinalizeAsync(request);
            if (CompletedSale is null)
            {
                return;
            }

            PrintedTaxId = request.ReceiptTaxId;
            DialogResult = true;
            Close();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Discount_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        var prompt = new DiscountPromptWindow(DiscountPercentInput)
        {
            Owner = this
        };

        if (prompt.ShowDialog() == true)
        {
            DiscountPercentInput = prompt.DiscountPercent.ToString("0.##");
        }
    }

    private void SelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        var lookup = new CustomerLookupWindow
        {
            Owner = this,
            Title = "Vincular cliente",
            DataContext = App.Services.GetRequiredService<CustomerLookupViewModel>()
        };

        if (lookup.ShowDialog() == true && lookup.SelectedCustomer is not null)
        {
            _selectedCustomerId = lookup.SelectedCustomer.Id;
            _selectedCustomerName = lookup.SelectedCustomer.Name;
            SelectedCustomerDisplay = string.IsNullOrWhiteSpace(lookup.SelectedCustomer.Cpf)
                ? lookup.SelectedCustomer.Name
                : $"{lookup.SelectedCustomer.Name} - {TextNormalization.FormatTaxIdPartial(lookup.SelectedCustomer.Cpf)}";
            OnPropertyChanged(nameof(CustomerButtonText));
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        // Mantido apenas por compatibilidade do XAML. Nao ha segunda etapa.
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
        if (Owner?.DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.CancelSaleShortcutLabel))
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
<<<<<<< HEAD
        else if (vm.MatchesShortcut(e.Key, vm.ChangePriceShortcutLabel))
        {
            Discount_Click(sender, e);
=======
        else if (vm.MatchesShortcut(e.Key, vm.OpenPaymentShortcutLabel))
        {
            FocusSelectedPayment();
>>>>>>> 499a3e3976bb49ef8087dcf2bfc67641afe3b3a5
            e.Handled = true;
        }
        else if (vm.MatchesShortcut(e.Key, vm.SelectCustomerShortcutLabel))
        {
            SelectCustomer_Click(sender, e);
            e.Handled = true;
        }
<<<<<<< HEAD
        else if (e.Key == Key.D1 || e.Key == Key.NumPad1)
        {
            CashOption.IsChecked = true;
            CashOption.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
        {
            PixOption.IsChecked = true;
            PixOption.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
        {
            CreditCardOption.IsChecked = true;
            CreditCardOption.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.D4 || e.Key == Key.NumPad4)
        {
            DebitCardOption.IsChecked = true;
            DebitCardOption.Focus();
=======
        else if (vm.MatchesShortcut(e.Key, vm.PrintReceiptShortcutLabel))
        {
            _ = FinalizeAsync(forcePrintReceipt: true);
>>>>>>> 499a3e3976bb49ef8087dcf2bfc67641afe3b3a5
            e.Handled = true;
        }
        else if (e.Key == Key.Up || e.Key == Key.Down)
        {
            if (MovePaymentSelection(e.Key == Key.Down ? 1 : -1))
            {
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            _ = FinalizeAsync(forcePrintReceipt: false);
            e.Handled = true;
        }
    }

    private void UpdateCashPreview()
    {
        RefreshSalePreview();
        if (!IsCashSelected)
        {
            CashValidationMessage = string.Empty;
            return;
        }

        _ = ValidateCashAmount();
    }

    private bool TryGetSelectedPaymentMethod(out PaymentMethod paymentMethod)
    {
        if (CashOption.IsChecked == true)
        {
            paymentMethod = PaymentMethod.Cash;
            return true;
        }

        if (PixOption.IsChecked == true)
        {
            paymentMethod = PaymentMethod.Pix;
            return true;
        }

        if (CreditCardOption.IsChecked == true)
        {
            paymentMethod = PaymentMethod.CreditCard;
            return true;
        }

        if (DebitCardOption.IsChecked == true)
        {
            paymentMethod = PaymentMethod.DebitCard;
            return true;
        }

        paymentMethod = default;
        return false;
    }

    private int? ValidateCashAmount()
    {
        if (!MoneyFormatter.TryParseToCents(ReceivedAmountInput, out var receivedAmountCents))
        {
            CashValidationMessage = "Informe um valor valido em dinheiro.";
            return null;
        }

        CashValidationMessage = string.Empty;
        RefreshSalePreview();
        return receivedAmountCents;
    }

    private void RefreshSalePreview()
    {
        OnPropertyChanged(nameof(SaleValueFormatted));
        OnPropertyChanged(nameof(DiscountPreviewFormatted));
        OnPropertyChanged(nameof(DiscountAppliedFormatted));
        OnPropertyChanged(nameof(HasDiscountApplied));
        OnPropertyChanged(nameof(TotalWithDiscountCents));
        OnPropertyChanged(nameof(TotalWithDiscountFormatted));
        OnPropertyChanged(nameof(ChangeFormatted));
        OnPropertyChanged(nameof(DiscountButtonText));
    }

<<<<<<< HEAD
=======
    private void FocusSelectedPayment()
    {
        var selected = GetSelectedPaymentOption() ?? CashOption;
        selected.IsChecked = true;
        selected.Focus();
    }

>>>>>>> 499a3e3976bb49ef8087dcf2bfc67641afe3b3a5
    private RadioButton? GetSelectedPaymentOption()
    {
        return GetPaymentOptions().FirstOrDefault(option => option.IsChecked == true);
    }

    private RadioButton[] GetPaymentOptions()
    {
        return [CashOption, PixOption, CreditCardOption, DebitCardOption];
    }

    private bool MovePaymentSelection(int direction)
    {
        if (ReferenceEquals(Keyboard.FocusedElement, ReceivedAmountTextBox))
        {
            return false;
        }

        var options = GetPaymentOptions();
        var currentIndex = Array.IndexOf(options, GetSelectedPaymentOption() ?? CashOption);
        var nextIndex = Math.Clamp(currentIndex + direction, 0, options.Length - 1);
        if (nextIndex == currentIndex && options[currentIndex].IsChecked == true)
        {
            return false;
        }

        options[nextIndex].IsChecked = true;
        options[nextIndex].Focus();
        return true;
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
