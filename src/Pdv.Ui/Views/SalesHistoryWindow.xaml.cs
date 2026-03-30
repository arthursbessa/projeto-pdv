using System.Windows;
using System.Windows.Input;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class SalesHistoryWindow : Window
{
    public SalesHistoryWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SalesHistoryViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private async void LoadHistory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SalesHistoryViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private void SalesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSaleDetails();
    }

    private void OpenSale_Click(object sender, RoutedEventArgs e)
    {
        OpenSaleDetails();
    }

    private void OpenSaleDetails()
    {
        if (DataContext is not SalesHistoryViewModel vm || vm.SelectedSale is null)
        {
            return;
        }

        new SaleDetailsWindow
        {
            Owner = this,
            DataContext = vm
        }.ShowDialog();
    }
}
