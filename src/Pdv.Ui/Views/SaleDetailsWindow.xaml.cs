using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Abstractions;
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

        var storeSettingsRepository = App.Services.GetRequiredService<IStoreSettingsRepository>();
        var settings = await storeSettingsRepository.GetCurrentAsync();
        _ = FiscalCouponPrinter.Print(
            this,
            vm.SelectedSaleDetails,
            settings?.StoreName ?? "LOJA",
            settings?.Address ?? string.Empty,
            settings?.Cnpj ?? string.Empty,
            settings?.LogoLocalPath ?? string.Empty);
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
