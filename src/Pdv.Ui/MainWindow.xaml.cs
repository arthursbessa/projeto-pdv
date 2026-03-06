using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pdv.Application.Domain;
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
        OpenFinalizeDialogAsync();
    }

    private void OpenFinalizeDialogAsync()
    {
        if (DataContext is not MainViewModel)
        {
            return;
        }

        var modal = new FinalizeSaleWindow { Owner = this };
        var result = modal.ShowDialog();

        if (result == true && modal.CompletedSale is not null && modal.ShouldPrintCoupon)
        {
            PrintFiscalCoupon(modal.CompletedSale);
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
            OpenFinalizeDialogAsync();
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
        else if (e.Key == Key.F6)
        {
            OpenQuantityDialog();
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
                    new TextBlock { Text = $"{vm.SelectedItem.Description}", FontWeight = FontWeights.SemiBold },
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

    private void PrintFiscalCoupon(Sale sale)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        var document = BuildFiscalCouponDocument(sale, printDialog.PrintableAreaWidth);
        printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Cupom Fiscal");
    }

    private FlowDocument BuildFiscalCouponDocument(Sale sale, double printableAreaWidth)
    {
        var cupomText = BuildFiscalCouponText(sale);
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            PagePadding = new Thickness(20),
            ColumnWidth = printableAreaWidth,
            TextAlignment = TextAlignment.Left
        };

        var logo = TryCreateBlackAndWhiteLogo();
        if (logo is not null)
        {
            document.Blocks.Add(new Paragraph(new InlineUIContainer(new Image
            {
                Source = logo,
                Width = 140,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            }))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        document.Blocks.Add(new Paragraph(new Run(cupomText))
        {
            Margin = new Thickness(0)
        });

        return document;
    }

    private BitmapSource? TryCreateBlackAndWhiteLogo()
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrWhiteSpace(vm.StoreLogoPath))
        {
            return null;
        }

        var fullPath = vm.StoreLogoPath;
        if (!Path.IsPathRooted(fullPath))
        {
            fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, fullPath));
        }

        if (!File.Exists(fullPath))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(fullPath, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        var blackAndWhite = new FormatConvertedBitmap();
        blackAndWhite.BeginInit();
        blackAndWhite.Source = image;
        blackAndWhite.DestinationFormat = PixelFormats.Gray8;
        blackAndWhite.EndInit();
        blackAndWhite.Freeze();

        return blackAndWhite;
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
