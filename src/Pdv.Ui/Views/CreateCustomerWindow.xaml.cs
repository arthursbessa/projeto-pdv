using System.Windows;
using System.Windows.Controls;
using Pdv.Application.Utilities;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CreateCustomerWindow : Window
{
    private bool _isFormattingCpf;

    public CreateCustomerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is CreateCustomerViewModel vm && !vm.IsEditMode)
            {
                vm.New();
            }
        };
    }

    public CreateCustomerViewModel? ViewModel => DataContext as CreateCustomerViewModel;

    private void CpfTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFormattingCpf || sender is not TextBox textBox)
        {
            return;
        }

        var formatted = TextNormalization.FormatTaxIdPartial(textBox.Text);
        if (textBox.Text == formatted)
        {
            return;
        }

        _isFormattingCpf = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _isFormattingCpf = false;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (await ViewModel.SaveAsync())
        {
            DialogResult = true;
            Close();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (await ViewModel.DeleteAsync())
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
