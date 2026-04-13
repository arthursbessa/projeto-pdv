using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Pdv.Ui.Views;

public partial class ReceiptPromptWindow : Window
{
    private bool _isFormatting;

    public ReceiptPromptWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PrintReceiptCheckBox.Focus();
    }

    public bool PrintReceipt { get; private set; }

    public string? ReceiptTaxId { get; private set; }

    private void PrintReceiptCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (PrintReceiptCheckBox.IsChecked == true)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TaxIdTextBox.Focus();
                TaxIdTextBox.SelectAll();
            });
        }
        else
        {
            TaxIdTextBox.Clear();
        }
    }

    private void TaxIdTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isFormatting)
        {
            return;
        }

        var digits = ExtractDigits(TaxIdTextBox.Text);
        if (digits.Length == 0)
        {
            return;
        }

        digits = digits.Length > 14 ? digits[..14] : digits;

        _isFormatting = true;
        var formatted = FormatDocument(digits);
        TaxIdTextBox.Text = formatted;
        TaxIdTextBox.SelectionStart = formatted.Length;
        _isFormatting = false;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var printReceipt = PrintReceiptCheckBox.IsChecked == true;
        var digits = ExtractDigits(TaxIdTextBox.Text);

        if (printReceipt && digits.Length is not 0 and not 11 and not 14)
        {
            MessageBox.Show(this, "Informe um CPF com 11 digitos ou um CNPJ com 14 digitos.");
            return;
        }

        PrintReceipt = printReceipt;
        ReceiptTaxId = printReceipt && digits.Length > 0 ? FormatDocument(digits) : null;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private static string ExtractDigits(string? value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value ?? string.Empty)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string FormatDocument(string digits)
    {
        digits = ExtractDigits(digits);

        if (digits.Length <= 11)
        {
            return FormatCpfPartial(digits);
        }

        return FormatCnpjPartial(digits);
    }

    private static string FormatCpfPartial(string digits)
    {
        digits = ExtractDigits(digits);
        if (digits.Length <= 3)
        {
            return digits;
        }

        if (digits.Length <= 6)
        {
            return $"{digits[..3]}.{digits[3..]}";
        }

        if (digits.Length <= 9)
        {
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..]}";
        }

        if (digits.Length <= 11)
        {
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
        }

        return digits[..11];
    }

    private static string FormatCnpjPartial(string digits)
    {
        digits = ExtractDigits(digits);
        if (digits.Length <= 2)
        {
            return digits;
        }

        if (digits.Length <= 5)
        {
            return $"{digits[..2]}.{digits[2..]}";
        }

        if (digits.Length <= 8)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..]}";
        }

        if (digits.Length <= 12)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..]}";
        }

        if (digits.Length <= 14)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
        }

        return digits[..14];
    }
}
