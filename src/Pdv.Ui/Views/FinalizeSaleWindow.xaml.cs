using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Pdv.Application.Domain;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class FinalizeSaleWindow : Window, INotifyPropertyChanged
{
    private bool _isBusy;

    public FinalizeSaleWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public PaymentMethod? SelectedPaymentMethod { get; private set; }
    public bool ShouldPrintCoupon { get; private set; }
    public Sale? CompletedSale { get; private set; }
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
        }
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

        ShouldPrintCoupon = PrintCouponOption.IsChecked == true;

        IsBusy = true;
        try
        {
            CompletedSale = await vm.FinalizeAsync(SelectedPaymentMethod.Value);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
