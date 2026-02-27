using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class OpenCashWindow : Window
{
    public OpenCashWindow()
    {
        InitializeComponent();
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MenuViewModel vm)
        {
            return;
        }

        await vm.OpenCashRegisterAsync(AmountTextBox.Text);
        DialogResult = true;
        Close();
    }
}
