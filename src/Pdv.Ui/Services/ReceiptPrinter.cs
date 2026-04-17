using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.Services;

public static class ReceiptPrinter
{
    public static bool Print(Window owner, Sale sale, string? receiptTaxId, StoreSettings? storeSettings, PdvSettings pdvSettings)
    {
        var document = BuildReceiptDocument(sale, receiptTaxId, 280, storeSettings, pdvSettings);

        if (pdvSettings.AskPrinterBeforePrint || string.IsNullOrWhiteSpace(pdvSettings.PreferredPrinterName))
        {
            var printDialog = new PrintDialog
            {
                UserPageRangeEnabled = false
            };

            if (printDialog.ShowDialog() != true)
            {
                return false;
            }

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Comprovante de venda");
            return true;
        }

        var localPrintServer = new LocalPrintServer();
        var queue = localPrintServer.GetPrintQueues()
            .FirstOrDefault(q => string.Equals(q.Name, pdvSettings.PreferredPrinterName, StringComparison.OrdinalIgnoreCase));

        if (queue is null)
        {
            MessageBox.Show(owner, "A impressora configurada nao foi encontrada. Revise as configuracoes de impressao.");
            return false;
        }

        var writer = PrintQueue.CreateXpsDocumentWriter(queue);
        writer.Write(((IDocumentPaginatorSource)document).DocumentPaginator);
        return true;
    }

    private static FlowDocument BuildReceiptDocument(Sale sale, string? receiptTaxId, double printableAreaWidth, StoreSettings? storeSettings, PdvSettings pdvSettings)
    {
        var receiptText = BuildReceiptText(sale, receiptTaxId, storeSettings, pdvSettings);
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            PagePadding = new Thickness(20),
            ColumnWidth = printableAreaWidth,
            TextAlignment = TextAlignment.Left
        };

        var logo = TryCreateBlackAndWhiteLogo(storeSettings?.LogoLocalPath);
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

        document.Blocks.Add(new Paragraph(new Run(receiptText))
        {
            Margin = new Thickness(0)
        });

        return document;
    }

    private static BitmapSource? TryCreateBlackAndWhiteLogo(string? storeLogoPath)
    {
        if (string.IsNullOrWhiteSpace(storeLogoPath))
        {
            return null;
        }

        var fullPath = storeLogoPath;
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

    private static string BuildReceiptText(Sale sale, string? receiptTaxId, StoreSettings? storeSettings, PdvSettings pdvSettings)
    {
        const int width = 44;
        var sb = new StringBuilder();

        var safeStoreName = string.IsNullOrWhiteSpace(storeSettings?.StoreName) ? "LOJA" : storeSettings!.StoreName;
        var safeStoreAddress = string.IsNullOrWhiteSpace(storeSettings?.Address) ? "ENDERECO NAO INFORMADO" : storeSettings!.Address;
        var safeStoreCnpj = string.IsNullOrWhiteSpace(storeSettings?.Cnpj)
            ? "NAO INFORMADO"
            : TextNormalization.FormatTaxIdPartial(storeSettings!.Cnpj);

        sb.AppendLine(Center(safeStoreName.ToUpperInvariant(), width));
        sb.AppendLine(Center(safeStoreAddress.ToUpperInvariant(), width));
        sb.AppendLine($"CNPJ:{safeStoreCnpj}");
        sb.AppendLine(new string('-', width));
        sb.AppendLine(Center("COMPROVANTE DE VENDA", width));
        if (!string.IsNullOrWhiteSpace(receiptTaxId))
        {
            sb.AppendLine($"CPF/CNPJ:{TextNormalization.FormatTaxIdPartial(receiptTaxId)}");
        }
        sb.AppendLine(new string('-', width));
        sb.AppendLine("ITEM CODIGO      DESCRICAO          VL ITEM");

        var index = 1;
        foreach (var item in sale.Items)
        {
            var productDescription = ProductTextFormatter.Format(item.Description, pdvSettings.ProductTextCase);
            var description = productDescription.Length > 16
                ? productDescription[..16]
                : productDescription;

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
        if (sale.DiscountCents > 0)
        {
            sb.AppendLine($"DESCONTO: R$ {FormatMoney(sale.DiscountCents)}");
        }
        sb.AppendLine($"TOTAL R$ {FormatMoney(sale.TotalCents)}");
        sb.AppendLine($"PAGAMENTO: {GetPaymentLabel(sale.PaymentMethod)}");

        if (sale.ReceivedAmountCents.HasValue)
        {
            sb.AppendLine($"RECEBIDO: R$ {FormatMoney(sale.ReceivedAmountCents.Value)}");
            sb.AppendLine($"TROCO:    R$ {FormatMoney(sale.ChangeAmountCents)}");
        }

        sb.AppendLine($"DATA: {sale.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"CONTROLE:{sale.SaleId.ToString()[..8].ToUpperInvariant()}");
        sb.AppendLine(new string('-', width));

        return sb.ToString();
    }

    private static string GetPaymentLabel(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash => "Dinheiro",
            PaymentMethod.CreditCard => "Cartao de credito",
            PaymentMethod.DebitCard => "Cartao de debito",
            PaymentMethod.Pix => "PIX",
            _ => paymentMethod.ToString()
        };
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
}
