using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CloseCashWindow : Window
{
    public CloseCashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MenuViewModel vm)
            {
                await vm.RefreshCashStatusAsync();
            }
        };
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MenuViewModel vm)
        {
            return;
        }

        await vm.CloseCashRegisterAsync();
        DialogResult = true;
        Close();
    }
}
