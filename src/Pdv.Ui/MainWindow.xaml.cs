using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.ViewModels;
using Pdv.Ui.Views;

namespace Pdv.Ui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => FocusBarcode();
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        await AddItemFromBarcodeAsync();
    }

    private async void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AddItemFromBarcodeAsync();
            e.Handled = true;
        }
    }

    private async Task AddItemFromBarcodeAsync()
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.AddBarcodeAsync();
        }

        FocusBarcode();
    }

    private async void Finalize_Click(object sender, RoutedEventArgs e)
    {
        await OpenFinalizeDialogAsync();
    }

    private async Task OpenFinalizeDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var modal = new FinalizeSaleWindow { Owner = this };
        var result = modal.ShowDialog();

        if (result == true && modal.SelectedPaymentMethod.HasValue)
        {
            await vm.FinalizeAsync(modal.SelectedPaymentMethod.Value);
        }

        FocusBarcode();
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            FocusBarcode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            await OpenFinalizeDialogAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F4)
        {
            vm.RemoveSelectedItem();
            FocusBarcode();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelSale();
            FocusBarcode();
            e.Handled = true;
        }
    }

    private void ItemsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Row.Item is not Pdv.Application.Domain.SaleItem item)
        {
            return;
        }

        if (e.Column.DisplayIndex != 2 || e.EditingElement is not TextBox textBox)
        {
            return;
        }

        if (!vm.UpdateItemQuantity(item, textBox.Text))
        {
            e.Cancel = true;
        }
        else
        {
            ItemsDataGrid.Items.Refresh();
        }

        FocusBarcode();
    }

    private void OpenProducts_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProductsWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<ProductsViewModel>()
        };
        window.ShowDialog();
        FocusBarcode();
    }

    private void FocusBarcode()
    {
        BarcodeTextBox.Focus();
        BarcodeTextBox.SelectAll();
    }
}
