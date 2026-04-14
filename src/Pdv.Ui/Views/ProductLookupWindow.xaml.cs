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
