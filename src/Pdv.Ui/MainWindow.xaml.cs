using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Domain;
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
                await vm.LoadStoreSettingsAsync();
            }

            FocusBarcode();
        };
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

    private void Finalize_Click(object sender, RoutedEventArgs e)
    {
        OpenFinalizeDialog();
    }

    private async void SearchProduct_Click(object sender, RoutedEventArgs e)
    {
        await OpenProductLookupAsync();
    }

    private async void SearchCustomer_Click(object sender, RoutedEventArgs e)
    {
        await OpenCustomerLookupAsync();
    }

    private void OpenFinalizeDialog()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var modal = new FinalizeSaleWindow { Owner = this };
        var result = modal.ShowDialog();

        if (result == true && modal.CompletedSale is not null && modal.ShouldPrintCoupon)
        {
            var printInfo = vm.GetStorePrintInfo();
            _ = FiscalCouponPrinter.Print(
                this,
                modal.CompletedSale,
                printInfo.StoreName,
                printInfo.StoreAddress,
                printInfo.StoreCnpj,
                printInfo.StoreLogoPath);
        }

        FocusBarcode();
    }

    private async Task OpenProductLookupAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var lookup = new ProductLookupWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<ProductLookupViewModel>()
        };

        if (lookup.ShowDialog() == true && lookup.SelectedProduct is not null)
        {
            await vm.AddProductByIdAsync(lookup.SelectedProduct.Id);
            ItemsDataGrid.Items.Refresh();
        }

        FocusBarcode();
    }

    private async Task OpenCustomerLookupAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var lookup = new CustomerLookupWindow
        {
            Owner = this,
            DataContext = App.Services.GetRequiredService<CustomerLookupViewModel>()
        };

        if (lookup.ShowDialog() == true && lookup.SelectedCustomer is not null)
        {
            await vm.SelectCustomerByIdAsync(lookup.SelectedCustomer.Id);
        }

        FocusBarcode();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
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

        if (e.Key == Key.F2)
        {
            OpenFinalizeDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            _ = OpenProductLookupAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F4)
        {
            vm.RemoveSelectedItem();
            FocusBarcode();
            e.Handled = true;
        }
        else if (e.Key == Key.F6)
        {
            OpenQuantityDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.F7)
        {
            _ = OpenCustomerLookupAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelSale();
            FocusBarcode();
            e.Handled = true;
        }
    }

    private void ChangeQuantity_Click(object sender, RoutedEventArgs e)
    {
        OpenQuantityDialog();
    }

    private void OpenQuantityDialog()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.SelectedItem is null)
        {
            vm.StatusMessage = "Selecione um item para alterar a quantidade.";
            return;
        }

        var quantityTextBox = new TextBox
        {
            Text = vm.SelectedItem.Quantity.ToString(),
            Margin = new Thickness(0, 10, 0, 0),
            MinWidth = 220
        };

        var dialog = new Window
        {
            Title = "Quantidade",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = vm.SelectedItem.Description, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = "Informe a quantidade:", Margin = new Thickness(0, 8, 0, 0) },
                    quantityTextBox,
                    new Button { Content = "Confirmar", Width = 110, Margin = new Thickness(0, 12, 0, 0), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children[^1] is Button confirm)
        {
            confirm.Click += (_, _) =>
            {
                if (!vm.UpdateSelectedItemQuantity(quantityTextBox.Text))
                {
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
            };
        }

        dialog.ShowDialog();
        ItemsDataGrid.Items.Refresh();
        FocusBarcode();
    }

    private void ItemsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Row.Item is not SaleItem item)
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

    private void FocusBarcode()
    {
        BarcodeTextBox.Focus();
        BarcodeTextBox.SelectAll();
    }
}
