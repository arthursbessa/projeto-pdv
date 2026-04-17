using System.Windows;
using Pdv.Ui.Services;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class SaleDetailsWindow : Window
{
    public SaleDetailsWindow()
    {
        InitializeComponent();
    }

    private async void Reprint_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SalesHistoryViewModel vm || vm.SelectedSaleDetails is null)
        {
            return;
        }

        var printContext = await vm.GetPrintContextAsync();
        ReceiptPrinter.Print(
            this,
            vm.SelectedSaleDetails,
            vm.SelectedSaleDetails.ReceiptTaxId,
            printContext.StoreSettings,
            printContext.Settings);
    }

    private async void Refund_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SalesHistoryViewModel vm)
        {
            return;
        }

        await vm.RegisterRefundAsync();
    }
}
