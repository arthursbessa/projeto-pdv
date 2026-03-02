using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Abstractions;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MenuViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private void OpenCashScreen_Click(object sender, RoutedEventArgs e)
    {
        new OpenCashWindow { Owner = this, DataContext = DataContext }.ShowDialog();
    }

    private void CloseCashScreen_Click(object sender, RoutedEventArgs e)
    {
        new CloseCashWindow { Owner = this, DataContext = DataContext }.ShowDialog();
    }

    private void OpenWithdrawalScreen_Click(object sender, RoutedEventArgs e)
    {
        new CashWithdrawalWindow { Owner = this, DataContext = DataContext }.ShowDialog();
    }

    private void OpenPdv_Click(object sender, RoutedEventArgs e)
    {
        var window = new MainWindow { Owner = this, DataContext = App.Services.GetRequiredService<MainViewModel>() };
        window.ShowDialog();
    }

    private async void OpenSales_Click(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<SessionContext>().OpenCashRegister;
        if (session is null)
        {
            MessageBox.Show("Não há caixa aberto.");
            return;
        }

        var repository = App.Services.GetRequiredService<ICashRegisterRepository>();
        var sales = await repository.GetSalesReportBySessionAsync(session.Id);
        new SalesReportWindow(sales) { Owner = this }.ShowDialog();
    }

    private async void OpenCashReport_Click(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<SessionContext>().OpenCashRegister;
        if (session is null)
        {
            MessageBox.Show("Não há caixa aberto.");
            return;
        }

        var repository = App.Services.GetRequiredService<ICashRegisterRepository>();
        var report = await repository.GetCashReportBySessionAsync(session.Id);
        new CashReportWindow(report) { Owner = this }.ShowDialog();
    }
}
