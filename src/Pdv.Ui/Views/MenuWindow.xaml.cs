using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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
        var session = App.Services.GetRequiredService<SessionContext>();
        if (session.OpenCashRegister is null)
        {
            MessageBox.Show("Nao e possivel abrir o PDV com o caixa fechado.");
            return;
        }

        var window = new MainWindow { Owner = this, DataContext = App.Services.GetRequiredService<MainViewModel>() };
        window.ShowDialog();
    }

    private void OpenIntegrations_Click(object sender, RoutedEventArgs e)
    {
        new IntegrationsWindow { Owner = this, DataContext = DataContext }.ShowDialog();
    }

    private void OpenSalesHistory_Click(object sender, RoutedEventArgs e)
    {
        new SalesHistoryWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<SalesHistoryViewModel>()
        }.ShowDialog();
    }
}
