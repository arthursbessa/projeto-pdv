using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Pdv.Application.Domain;
using Pdv.Ui.Views;
using Pdv.Ui.ViewModels;

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
                await vm.RefreshCatalogAsync();
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
            var sale = await vm.FinalizeAsync(modal.SelectedPaymentMethod.Value);
            if (sale is not null && modal.ShouldPrintCoupon)
            {
                PrintFiscalCoupon(sale);
            }
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

    private void PrintFiscalCoupon(Sale sale)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        var cupom = BuildFiscalCouponText(sale);
        var document = new FlowDocument(new Paragraph(new Run(cupom)))
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            PagePadding = new Thickness(20),
            ColumnWidth = printDialog.PrintableAreaWidth
        };

        printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Cupom Fiscal");
    }

    private string BuildFiscalCouponText(Sale sale)
    {
        const int width = 44;
        var sb = new StringBuilder();

        var vm = DataContext as MainViewModel;
        var storeName = string.IsNullOrWhiteSpace(vm?.StoreName) ? "LOJA" : vm!.StoreName;
        var storeAddress = string.IsNullOrWhiteSpace(vm?.StoreAddress) ? "ENDEREÇO NÃO INFORMADO" : vm!.StoreAddress;
        var storeCnpj = string.IsNullOrWhiteSpace(vm?.StoreCnpj) ? "NÃO INFORMADO" : vm!.StoreCnpj;

        sb.AppendLine(Center(storeName.ToUpperInvariant(), width));
        sb.AppendLine(Center(storeAddress.ToUpperInvariant(), width));
        sb.AppendLine($"CNPJ:{storeCnpj}");
        sb.AppendLine(new string('-', width));
        sb.AppendLine(Center("CUPOM FISCAL", width));
        sb.AppendLine(new string('-', width));
        sb.AppendLine("ITEM CODIGO      DESCRICAO          VL ITEM");

        var index = 1;
        foreach (var item in sale.Items)
        {
            var description = item.Description.Length > 16
                ? item.Description[..16]
                : item.Description;

            var line = string.Format("{0:000} {1,-11} {2,-16} {3,8}",
                index,
                item.Barcode.Length > 11 ? item.Barcode[..11] : item.Barcode,
                description,
                FormatMoney(item.SubtotalCents));

            sb.AppendLine(line);
            sb.AppendLine($"    {item.Quantity}UN X {FormatMoney(item.PriceCents)}");
            index++;
        }

        sb.AppendLine(new string('-', width));
        sb.AppendLine($"TOTAL R$ {FormatMoney(sale.TotalCents)}");
        sb.AppendLine($"PAGAMENTO: {sale.PaymentMethod}");
        sb.AppendLine($"DATA: {sale.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"CONTROLE:{sale.SaleId.ToString()[..8].ToUpperInvariant()}");
        sb.AppendLine(new string('-', width));

        return sb.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width)
        {
            return text;
        }

        var leftPadding = (width - text.Length) / 2;
        return new string(' ', leftPadding) + text;
    }

    private static string FormatMoney(int valueInCents)
    {
        return (valueInCents / 100m).ToString("N2");
    }

    private void FocusBarcode()
    {
        BarcodeTextBox.Focus();
        BarcodeTextBox.SelectAll();
    }
}
