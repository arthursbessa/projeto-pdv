using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Domain;
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

    private void QueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is CustomerLookupViewModel vm && vm.SelectedCustomer is null && CustomersDataGrid.Items.Count > 0)
            {
                CustomersDataGrid.SelectedIndex = 0;
            }

            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && CustomersDataGrid.Items.Count > 0)
        {
            if (CustomersDataGrid.SelectedIndex < 0)
            {
                CustomersDataGrid.SelectedIndex = 0;
            }

            CustomersDataGrid.Focus();
            e.Handled = true;
        }
    }

    private void CustomersDataGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (CustomersDataGrid.SelectedIndex < 0 && CustomersDataGrid.Items.Count > 0)
            {
                CustomersDataGrid.SelectedIndex = 0;
            }

            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private void CustomersDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (CustomersDataGrid.SelectedIndex < 0 && CustomersDataGrid.Items.Count > 0)
        {
            CustomersDataGrid.SelectedIndex = 0;
        }

        ConfirmSelection();
        e.Handled = true;
    }

    private async void CreateCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CustomerLookupViewModel vm)
        {
            return;
        }

        var editorVm = App.Services.GetRequiredService<CreateCustomerViewModel>();
        editorVm.New();
        var createWindow = new CreateCustomerWindow
        {
            Owner = this,
            DataContext = editorVm
        };

        if (createWindow.ShowDialog() == true && createWindow.ViewModel?.CreatedCustomer is not null)
        {
            await vm.AddCreatedCustomerAsync(createWindow.ViewModel.CreatedCustomer);
        }
    }

    private async void EditCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not CustomerLookupItemViewModel item)
        {
            return;
        }

        if (DataContext is not CustomerLookupViewModel vm)
        {
            return;
        }

        var customer = new CustomerRecord
        {
            Id = item.Id,
            Name = item.Name,
            Cpf = item.Cpf,
            Phone = item.Phone,
            Email = item.Email
        };

        var editorVm = App.Services.GetRequiredService<CreateCustomerViewModel>();
        editorVm.LoadExisting(customer);
        var createWindow = new CreateCustomerWindow
        {
            Owner = this,
            DataContext = editorVm
        };

        if (createWindow.ShowDialog() == true && createWindow.ViewModel?.CreatedCustomer is not null)
        {
            await vm.LoadAsync();
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
