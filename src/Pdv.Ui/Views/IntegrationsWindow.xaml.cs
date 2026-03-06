using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class IntegrationsWindow : Window
{
    public IntegrationsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MenuViewModel vm)
            {
                await vm.RefreshIntegrationStatusesAsync();
            }
        };
    }

    private async void IntegrateData_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MenuViewModel vm)
        {
            await vm.IntegratePendingSalesAsync();
            await vm.RefreshIntegrationStatusesAsync();
        }
    }
}
