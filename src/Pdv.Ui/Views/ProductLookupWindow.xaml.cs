using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class ProductLookupWindow : Window
{
    public ProductLookupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ProductLookupViewModel vm)
            {
                await vm.LoadAsync();
                QueryTextBox.Focus();
                QueryTextBox.SelectAll();
            }
        };
    }

    public ProductLookupItemViewModel? SelectedProduct { get; private set; }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void ProductsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
            if (DataContext is ProductLookupViewModel vm && vm.SelectedProduct is null && ProductsDataGrid.Items.Count > 0)
            {
                ProductsDataGrid.SelectedIndex = 0;
            }

            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ProductsDataGrid.Items.Count > 0)
        {
            if (ProductsDataGrid.SelectedIndex < 0)
            {
                ProductsDataGrid.SelectedIndex = 0;
            }

            ProductsDataGrid.Focus();
            e.Handled = true;
        }
    }

    private void ProductsDataGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ProductsDataGrid.SelectedIndex < 0 && ProductsDataGrid.Items.Count > 0)
            {
                ProductsDataGrid.SelectedIndex = 0;
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

    private void ProductsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (ProductsDataGrid.SelectedIndex < 0 && ProductsDataGrid.Items.Count > 0)
        {
            ProductsDataGrid.SelectedIndex = 0;
        }

        ConfirmSelection();
        e.Handled = true;
    }

    private void ConfirmSelection()
    {
        if (DataContext is not ProductLookupViewModel vm || vm.SelectedProduct is null)
        {
            return;
        }

        SelectedProduct = vm.SelectedProduct;
        DialogResult = true;
        Close();
    }

    private async void CreateProduct_Click(object sender, RoutedEventArgs e)
    {
        var vm = App.Services.GetRequiredService<ProductsViewModel>();
        vm.New();
        var window = new ProductsWindow
        {
            Owner = this,
            DataContext = vm
        };

        window.ShowDialog();
        if (DataContext is ProductLookupViewModel lookupVm)
        {
            await lookupVm.LoadAsync();
        }
    }

    private async void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ProductLookupItemViewModel item)
        {
            return;
        }

        var vm = App.Services.GetRequiredService<ProductsViewModel>();
        await vm.OpenExistingAsync(item.Id);
        var window = new ProductsWindow
        {
            Owner = this,
            DataContext = vm
        };

        window.ShowDialog();
        if (DataContext is ProductLookupViewModel currentLookupVm)
        {
            await currentLookupVm.LoadAsync();
        }
    }
}
