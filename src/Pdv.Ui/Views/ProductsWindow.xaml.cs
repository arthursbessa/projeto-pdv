using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class ProductsWindow : System.Windows.Window
{
    public ProductsWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductsViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel vm)
        {
            return;
        }

        if (await vm.SaveAsync())
        {
            DialogResult = true;
            Close();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel vm)
        {
            return;
        }

        if (await vm.DeleteAsync())
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void CreateCategory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel vm)
        {
            return;
        }

        var categoryVm = App.Services.GetRequiredService<CreateCategoryViewModel>();
        var window = new CreateCategoryWindow
        {
            Owner = this,
            DataContext = categoryVm
        };

        if (window.ShowDialog() == true && window.ViewModel?.CreatedCategory is not null)
        {
            vm.CategoryId = window.ViewModel.CreatedCategory.Id;
            await vm.LoadAsync();
        }
    }

    private async void CreateSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel vm)
        {
            return;
        }

        var supplierVm = App.Services.GetRequiredService<CreateSupplierViewModel>();
        var window = new CreateSupplierWindow
        {
            Owner = this,
            DataContext = supplierVm
        };

        if (window.ShowDialog() == true && window.ViewModel?.CreatedSupplier is not null)
        {
            vm.SupplierId = window.ViewModel.CreatedSupplier.Id;
            await vm.LoadAsync();
        }
    }
}
