using System.Windows;
using System.Windows.Input;
using Pdv.Application.Domain;

namespace Pdv.Ui.Views;

public partial class FinalizeSaleWindow : Window
{
    public FinalizeSaleWindow()
    {
        InitializeComponent();
    }

    public PaymentMethod? SelectedPaymentMethod { get; private set; }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaymentMethod = CashOption.IsChecked == true
            ? PaymentMethod.Cash
            : CardOption.IsChecked == true
                ? PaymentMethod.Card
                : PaymentMethod.Pix;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
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
}
