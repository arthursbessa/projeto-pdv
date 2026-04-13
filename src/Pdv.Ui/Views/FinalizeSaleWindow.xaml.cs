using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
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
    private int _currentStep = 1;
    private string _discountPercentInput = "0";
    private string _selectedCustomerDisplay = "Nenhum cliente selecionado";
    private string? _selectedCustomerId;
    private string? _selectedCustomerName;
    private bool _hasCustomer = false;

    public FinalizeSaleWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            if (Owner?.DataContext is MainViewModel vm)
            {
                _totalCents = vm.TotalCents;
                DiscountPercentInput = vm.DefaultDiscountPercent > 0
                    ? vm.DefaultDiscountPercent.ToString("0.##")
                    : "0";
                OnPropertyChanged(nameof(SubtotalFormatted));
                OnPropertyChanged(nameof(TotalWithDiscountFormatted));
                OnPropertyChanged(nameof(DiscountPreviewFormatted));
                ReceivedAmountInput = (TotalWithDiscountCents / 100m).ToString("F2");
            }

            DiscountTextBox.Focus();
            DiscountTextBox.SelectAll();
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
                OnPropertyChanged(nameof(TotalWithDiscountFormatted));
                OnPropertyChanged(nameof(TotalWithDiscountCents));
                // Recalcula troco com o novo desconto
                UpdateCashPreview();
            }
        }
    }

    public string SubtotalFormatted => MoneyFormatter.FormatFromCents(_totalCents);

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

    public int TotalWithDiscountCents => Math.Max(_totalCents - DiscountPreviewCents, 0);

    public string DiscountPreviewFormatted => MoneyFormatter.FormatFromCents(DiscountPreviewCents);
    public string TotalWithDiscountFormatted => MoneyFormatter.FormatFromCents(TotalWithDiscountCents);

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

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetField(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStepOneVisible));
                OnPropertyChanged(nameof(IsStepTwoVisible));
                OnPropertyChanged(nameof(StepOneForeground));
                OnPropertyChanged(nameof(StepTwoForeground));
                OnPropertyChanged(nameof(PrimaryActionText));
                OnPropertyChanged(nameof(IsBackVisible));
                OnPropertyChanged(nameof(Step1DotColor));
                OnPropertyChanged(nameof(Step2DotColor));
                OnPropertyChanged(nameof(StepLabel));
            }
        }
    }

    public bool IsStepOneVisible => CurrentStep == 1;
    public bool IsStepTwoVisible => CurrentStep == 2;
    public bool IsBackVisible => CurrentStep > 1;

    public Brush StepOneForeground => CurrentStep == 1 ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 23, 42));
    public Brush StepTwoForeground => CurrentStep == 2 ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 23, 42));

    private static readonly Brush DotActive = new SolidColorBrush(Color.FromRgb(31, 95, 191));
    private static readonly Brush DotDone = new SolidColorBrush(Color.FromRgb(147, 197, 253));
    private static readonly Brush DotIdle = new SolidColorBrush(Color.FromRgb(203, 213, 225));

    public Brush Step1DotColor => CurrentStep >= 1 ? (CurrentStep == 1 ? DotActive : DotDone) : DotIdle;
    public Brush Step2DotColor => CurrentStep >= 2 ? (CurrentStep == 2 ? DotActive : DotDone) : DotIdle;
    public string StepLabel => CurrentStep switch
    {
        1 => "Desconto",
        _ => "Pagamento"
    };

    public string PrimaryActionText => CurrentStep switch
    {
        1 => "Proximo",
        _ => "Finalizar venda"
    };

    public string SelectedCustomerDisplay
    {
        get => _selectedCustomerDisplay;
        private set => SetField(ref _selectedCustomerDisplay, value);
    }

    public bool HasCustomer
    {
        get => _hasCustomer;
        private set => SetField(ref _hasCustomer, value);
    }

    private void PaymentOption_Checked(object sender, RoutedEventArgs e)
    {
        IsCashSelected = sender == CashOption;
        CashValidationMessage = string.Empty;

        if (IsCashSelected)
        {
            // Atualiza o valor sugerido com o total já com desconto
            ReceivedAmountInput = (TotalWithDiscountCents / 100m).ToString("F2");
            Dispatcher.BeginInvoke(() =>
            {
                ReceivedAmountTextBox.Focus();
                ReceivedAmountTextBox.SelectAll();
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(ReceivedAmountInput))
        {
            ReceivedAmountInput = string.Empty;
        }

        OnPropertyChanged(nameof(ChangeFormatted));
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentStep == 1)
        {
            if (!ValidateDiscount())
            {
                return;
            }

            CurrentStep = 2;
            return;
        }

        if (CurrentStep == 2)
        {
            if (IsCashSelected && !ValidateCashAmount().HasValue)
            {
                return;
            }

            var receiptPrompt = new ReceiptPromptWindow
            {
                Owner = this
            };

            if (receiptPrompt.ShowDialog() != true)
            {
                return;
            }

            if (Owner?.DataContext is not MainViewModel vm)
            {
                return;
            }

            if (!decimal.TryParse(
                    DiscountPercentInput.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var discountPercent))
            {
                MessageBox.Show(this, "Informe um desconto percentual valido.");
                return;
            }

            var paymentMethod = CashOption.IsChecked == true
                ? PaymentMethod.Cash
                : CreditCardOption.IsChecked == true
                    ? PaymentMethod.CreditCard
                    : DebitCardOption.IsChecked == true
                        ? PaymentMethod.DebitCard
                        : PaymentMethod.Pix;

            var request = new SaleCheckoutRequest
            {
                PaymentMethod = paymentMethod,
                ReceivedAmountCents = paymentMethod == PaymentMethod.Cash ? ValidateCashAmount() : null,
                CustomerId = _selectedCustomerId,
                CustomerName = _selectedCustomerName,
                DiscountPercent = discountPercent,
                ReceiptRequested = receiptPrompt.PrintReceipt,
                ReceiptTaxId = receiptPrompt.ReceiptTaxId
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
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (CurrentStep > 1)
        {
            CurrentStep--;
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
            PrimaryAction_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private void SelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        var lookup = new CustomerLookupWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<CustomerLookupViewModel>()
        };

        if (lookup.ShowDialog() == true && lookup.SelectedCustomer is not null)
        {
            _selectedCustomerId = lookup.SelectedCustomer.Id;
            _selectedCustomerName = lookup.SelectedCustomer.Name;
            SelectedCustomerDisplay = string.IsNullOrWhiteSpace(lookup.SelectedCustomer.Cpf)
                ? lookup.SelectedCustomer.Name
                : $"{lookup.SelectedCustomer.Name} - {lookup.SelectedCustomer.Cpf}";
            HasCustomer = true;
        }
    }

    private void ClearCustomer_Click(object sender, RoutedEventArgs e)
    {
        _selectedCustomerId = null;
        _selectedCustomerName = null;
        SelectedCustomerDisplay = "Nenhum cliente selecionado";
        HasCustomer = false;
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

    private bool ValidateDiscount()
    {
        if (!decimal.TryParse(
                DiscountPercentInput.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var percent))
        {
            MessageBox.Show(this, "Informe um valor de desconto valido (ex: 0, 5, 10).");
            return false;
        }

        if (percent < 0 || percent > 100)
        {
            MessageBox.Show(this, "O desconto deve ser entre 0% e 100%.");
            return false;
        }

        return true;
    }

    private int? ValidateCashAmount()
    {
        if (!MoneyFormatter.TryParseToCents(ReceivedAmountInput, out var receivedAmountCents))
        {
            CashValidationMessage = "Informe um valor valido em dinheiro.";
            return null;
        }

        if (receivedAmountCents < TotalWithDiscountCents)
        {
            CashValidationMessage = "Valor insuficiente para cobrir o total.";
            return null;
        }

        CashValidationMessage = string.Empty;
        OnPropertyChanged(nameof(ChangeFormatted));
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
