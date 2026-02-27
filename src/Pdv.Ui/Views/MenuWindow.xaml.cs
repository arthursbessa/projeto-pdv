using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Abstractions;
using Pdv.Ui.Formatting;
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

    private async void OpenCash_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MenuViewModel vm)
        {
            await vm.OpenCashRegisterAsync();
        }
    }

    private async void CloseCash_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MenuViewModel vm)
        {
            await vm.CloseCashRegisterAsync();
        }
    }

    private void OpenPdv_Click(object sender, RoutedEventArgs e)
    {
        var window = new MainWindow { Owner = this, DataContext = App.Services.GetRequiredService<MainViewModel>() };
        window.ShowDialog();
    }

    private void OpenProducts_Click(object sender, RoutedEventArgs e)
    {
        new ProductsWindow { Owner = this, DataContext = App.Services.GetRequiredService<ProductsViewModel>() }.ShowDialog();
    }

    private void OpenUsers_Click(object sender, RoutedEventArgs e)
    {
        new UsersWindow { Owner = this, DataContext = App.Services.GetRequiredService<UsersViewModel>() }.ShowDialog();
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
        var sales = await repository.GetSalesBySessionAsync(session.Id);
        var lines = sales.Any()
            ? string.Join(Environment.NewLine, sales.Select(x => $"{x.CreatedAt:HH:mm} | {x.PaymentMethod} | {MoneyFormatter.FormatFromCents(x.TotalCents)}"))
            : "Sem vendas para o caixa atual.";

        MessageBox.Show(lines, "Vendas do caixa aberto");
    }
}
