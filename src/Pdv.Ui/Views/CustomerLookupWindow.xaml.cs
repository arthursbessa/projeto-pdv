using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CustomerLookupWindow : Window
{
    public CustomerLookupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is CustomerLookupViewModel vm)
            {
                await vm.LoadAsync();
                QueryTextBox.Focus();
                QueryTextBox.SelectAll();
            }
        };
    }

    public CustomerLookupItemViewModel? SelectedCustomer { get; private set; }

    private void SelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void CustomersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private async void CreateCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CustomerLookupViewModel vm)
        {
            return;
        }

        var createWindow = new CreateCustomerWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<CreateCustomerViewModel>()
        };

        if (createWindow.ShowDialog() == true && createWindow.ViewModel?.CreatedCustomer is not null)
        {
            await vm.AddCreatedCustomerAsync(createWindow.ViewModel.CreatedCustomer);
        }
    }

    private void ConfirmSelection()
    {
        if (DataContext is not CustomerLookupViewModel vm || vm.SelectedCustomer is null)
        {
            return;
        }

        SelectedCustomer = vm.SelectedCustomer;
        DialogResult = true;
        Close();
    }
}
