using System.Windows;
using System.Windows.Input;
using Pdv.Application.Utilities;

namespace Pdv.Ui.Views;

public partial class ReceiptPromptWindow : Window
{
    private bool _isFormatting;

    public ReceiptPromptWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TaxIdTextBox.Focus();
    }

    public string? ReceiptTaxId { get; private set; }

    private void TaxIdTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isFormatting)
        {
            return;
        }

        var digits = TextNormalization.DigitsOnly(TaxIdTextBox.Text);
        if (digits.Length == 0)
        {
            return;
        }

        _isFormatting = true;
        TaxIdTextBox.Text = TextNormalization.FormatTaxIdPartial(digits);
        TaxIdTextBox.SelectionStart = TaxIdTextBox.Text.Length;
        _isFormatting = false;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var digits = TextNormalization.DigitsOnly(TaxIdTextBox.Text);
        if (digits.Length is not 0 and not 11 and not 14)
        {
            MessageBox.Show(this, "Informe um CPF com 11 digitos ou um CNPJ com 14 digitos.");
            return;
        }

        ReceiptTaxId = TextNormalization.FormatTaxId(TaxIdTextBox.Text);
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

}
