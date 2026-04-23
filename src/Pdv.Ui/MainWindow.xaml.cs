using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.Services;
using Pdv.Ui.ViewModels;
using Pdv.Ui.Views;

namespace Pdv.Ui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.LoadAsync();
            }

            SyncSelectionEditors();
            FocusBarcode();
        };
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        await AddItemFromBarcodeAsync();
    }

    private async void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.MatchesShortcut(e.Key, vm.AddItemShortcutLabel))
        {
            await AddItemFromBarcodeAsync();
            e.Handled = true;
        }
    }

    private async void CancelSale_Click(object sender, RoutedEventArgs e)
    {
        await CancelSaleWithConfirmationAsync();
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            FocusBarcode();
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.SearchProductShortcutLabel))
        {
            FocusBarcode();
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.ChangeQuantityShortcutLabel))
        {
            FocusSelectedQuantity();
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.ChangePriceShortcutLabel))
        {
            FocusSelectedPrice();
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.OpenPaymentShortcutLabel))
        {
            await OpenFinalizeDialogAsync();
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.ReprintLastSaleShortcutLabel))
        {
            await PrintLastSaleAsync(preferCurrentSession: false);
            e.Handled = true;
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.PrintReceiptShortcutLabel))
        {
            await PrintLastSaleAsync(preferCurrentSession: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            if (SelectRelativeItem(e.Key == Key.Down ? 1 : -1))
            {
                e.Handled = true;
            }
        }
    }

    private async Task AddItemFromBarcodeAsync()
    {
        if (DataContext is MainViewModel vm)
        {
            var barcode = vm.BarcodeInput?.Trim() ?? string.Empty;
            var added = await vm.AddBarcodeAsync();
            if (!added && !string.IsNullOrWhiteSpace(barcode))
            {
                await OpenProductLookupAsync(barcode);
                return;
            }

            ItemsDataGrid.Items.Refresh();
            SyncSelectionEditors();
        }

        FocusBarcode();
    }

    private async void Finalize_Click(object sender, RoutedEventArgs e)
    {
        await OpenFinalizeDialogAsync();
    }

    private async void SearchProduct_Click(object sender, RoutedEventArgs e)
    {
        await OpenProductLookupAsync(BarcodeTextBox.Text);
    }

    private async Task OpenFinalizeDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var modal = new FinalizeSaleWindow { Owner = this };
        var result = modal.ShowDialog();

        if (result == true && modal.CompletedSale is not null && modal.CompletedSale.ReceiptRequested)
        {
            var printContext = await vm.GetPrintContextAsync();
            ReceiptPrinter.Print(this, modal.CompletedSale, modal.PrintedTaxId, printContext.StoreSettings, printContext.Settings);
        }

        FocusBarcode();
    }

    private async Task OpenProductLookupAsync(string? initialQuery = null)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var lookupViewModel = App.Services.GetRequiredService<ProductLookupViewModel>();
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            lookupViewModel.Query = initialQuery.Trim();
        }

        var lookup = new ProductLookupWindow
        {
            Owner = this,
            DataContext = lookupViewModel
        };

        if (lookup.ShowDialog() == true && lookup.SelectedProduct is not null)
        {
            await vm.AddProductByIdAsync(lookup.SelectedProduct.Id);
            ItemsDataGrid.Items.Refresh();
            SyncSelectionEditors();
        }

        FocusBarcode();
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.MatchesShortcut(e.Key, vm.AddItemShortcutLabel))
        {
            await AddItemFromBarcodeAsync();
            e.Handled = true;
        }
        else if (vm.MatchesShortcut(e.Key, vm.FinalizeShortcutLabel))
        {
            await OpenFinalizeDialogAsync();
            e.Handled = true;
        }
        else if (vm.MatchesShortcut(e.Key, vm.RemoveItemShortcutLabel))
        {
            vm.RemoveSelectedItem();
            ItemsDataGrid.Items.Refresh();
            SyncSelectionEditors();
            FocusBarcode();
            e.Handled = true;
        }
        else if (vm.MatchesShortcut(e.Key, vm.CancelSaleShortcutLabel))
        {
            await CancelSaleWithConfirmationAsync();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.Items.Any())
        {
            return;
        }

        var choice = MessageBox.Show(
            this,
            "Existe uma venda em aberto. Deseja fechar esta tela mesmo assim?",
            "Fechar tela do PDV",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (choice != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void ItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectionEditors();
    }

    private void SelectedQuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem is null)
        {
            return;
        }

        if (SelectedQuantityTextBox.Text == vm.SelectedItem.Quantity.ToString())
        {
            return;
        }

        if (vm.UpdateItemQuantity(vm.SelectedItem, SelectedQuantityTextBox.Text))
        {
            ItemsDataGrid.Items.Refresh();
        }

        SyncSelectionEditors();
        FocusBarcode();
    }

    private async void SelectedPriceTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem is null)
        {
            return;
        }

        var currentFormatted = vm.SelectedItem.Price.ToString("0.00", CultureInfo.InvariantCulture);
        var proposedFormatted = SelectedPriceTextBox.Text.Trim().Replace(',', '.');
        if (proposedFormatted == currentFormatted)
        {
            return;
        }

        var choice = MessageBox.Show(
            this,
            "Deseja atualizar esse novo valor tambem no cadastro do produto?",
            "Atualizar preco do produto",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choice == MessageBoxResult.Cancel)
        {
            SyncSelectionEditors();
            return;
        }

        await vm.UpdateItemPriceAsync(vm.SelectedItem, SelectedPriceTextBox.Text, choice == MessageBoxResult.Yes);
        ItemsDataGrid.Items.Refresh();
        SyncSelectionEditors();
        FocusBarcode();
    }

    private void SelectedQuantityTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SelectedQuantityTextBox_LostFocus(sender, e);
            e.Handled = true;
        }
    }

    private void SelectedPriceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SelectedPriceTextBox_LostFocus(sender, e);
            e.Handled = true;
        }
    }

    private void SyncSelectionEditors()
    {
        if (DataContext is not MainViewModel vm)
        {
            SelectedPriceTextBox.Text = string.Empty;
            SelectedQuantityTextBox.Text = string.Empty;
            return;
        }

        if (vm.SelectedItem is null)
        {
            SelectedPriceTextBox.Text = vm.SelectedItemUnitPriceFormatted;
            SelectedQuantityTextBox.Text = vm.SelectedItemQuantityFormatted;
            return;
        }

        SelectedPriceTextBox.Text = vm.SelectedItem.Price.ToString("N2");
        SelectedQuantityTextBox.Text = vm.SelectedItem.Quantity.ToString();
    }

    private void FocusBarcode()
    {
        BarcodeTextBox.Focus();
        BarcodeTextBox.SelectAll();
    }

    private void FocusSelectedQuantity()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem is null)
        {
            return;
        }

        SelectedQuantityTextBox.Focus();
        SelectedQuantityTextBox.SelectAll();
    }

    private void FocusSelectedPrice()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem is null)
        {
            return;
        }

        SelectedPriceTextBox.Focus();
        SelectedPriceTextBox.SelectAll();
    }

    private bool SelectRelativeItem(int direction)
    {
        if (DataContext is not MainViewModel vm || vm.Items.Count == 0)
        {
            return false;
        }

        if (ReferenceEquals(Keyboard.FocusedElement, BarcodeTextBox)
            || ReferenceEquals(Keyboard.FocusedElement, SelectedPriceTextBox)
            || ReferenceEquals(Keyboard.FocusedElement, SelectedQuantityTextBox))
        {
            return false;
        }

        var currentIndex = vm.SelectedItem is null ? -1 : vm.Items.IndexOf(vm.SelectedItem);
        var nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : vm.Items.Count - 1)
            : Math.Clamp(currentIndex + direction, 0, vm.Items.Count - 1);

        if (nextIndex < 0 || nextIndex >= vm.Items.Count)
        {
            return false;
        }

        ItemsDataGrid.SelectedItem = vm.Items[nextIndex];
        ItemsDataGrid.ScrollIntoView(vm.Items[nextIndex]);
        SyncSelectionEditors();
        return true;
    }

    private async void ReprintLastSale_Click(object sender, RoutedEventArgs e)
    {
        await PrintLastSaleAsync(preferCurrentSession: false);
    }

    private async void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        await PrintLastSaleAsync(preferCurrentSession: true);
    }

    private async Task PrintLastSaleAsync(bool preferCurrentSession)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var sale = await vm.GetLastSaleForPrintAsync(preferCurrentSession);
        if (sale is null)
        {
            vm.StatusMessage = "Nenhuma venda concluida foi encontrada para impressao.";
            FocusBarcode();
            return;
        }

        var printContext = await vm.GetPrintContextAsync();
        ReceiptPrinter.Print(this, sale, sale.ReceiptTaxId, printContext.StoreSettings, printContext.Settings);
        vm.StatusMessage = preferCurrentSession
            ? "Comprovante da ultima venda do caixa enviado para impressao."
            : "Ultima venda concluida enviada para reimpressao.";
        FocusBarcode();
    }

    private Task CancelSaleWithConfirmationAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return Task.CompletedTask;
        }

        if (!vm.Items.Any())
        {
            vm.CancelSale();
            ItemsDataGrid.Items.Refresh();
            SyncSelectionEditors();
            FocusBarcode();
            return Task.CompletedTask;
        }

        var choice = MessageBox.Show(
            this,
            "Existe uma venda em aberto. Deseja cancelar o comprovante atual?",
            "Cancelar comprovante",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (choice != MessageBoxResult.Yes)
        {
            return Task.CompletedTask;
        }

        vm.CancelSale();
        ItemsDataGrid.Items.Refresh();
        SyncSelectionEditors();
        FocusBarcode();
        return Task.CompletedTask;
    }
}
