using System.Windows;
using System.Windows.Controls;
using Pdv.Application.Utilities;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CreateSupplierWindow : Window
{
    private bool _isFormattingCnpj;

    public CreateSupplierWindow()
    {
        InitializeComponent();
    }

    public CreateSupplierViewModel? ViewModel => DataContext as CreateSupplierViewModel;

    private void CnpjTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFormattingCnpj || sender is not TextBox textBox)
        {
            return;
        }

        var formatted = TextNormalization.FormatTaxIdPartial(textBox.Text);
        if (textBox.Text == formatted)
        {
            return;
        }

        _isFormattingCnpj = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _isFormattingCnpj = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CreateSupplierViewModel vm)
        {
            vm.New();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateSupplierViewModel vm)
        {
            return;
        }

        if (await vm.SaveAsync())
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
}
