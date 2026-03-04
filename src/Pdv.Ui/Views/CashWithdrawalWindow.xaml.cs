using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CashWithdrawalWindow : Window
{
    public CashWithdrawalWindow()
    {
        InitializeComponent();
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MenuViewModel vm)
        {
            return;
        }

        var success = await vm.RegisterWithdrawalAsync(AmountTextBox.Text, ReasonTextBox.Text);
        if (!success)
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
