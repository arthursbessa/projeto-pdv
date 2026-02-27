using System.Windows.Input;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BarcodeTextBox.Focus();
    }

    private async void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
        {
            await vm.AddBarcodeAsync();
            BarcodeTextBox.Focus();
        }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Key == Key.F2)
        {
            await vm.FinalizeAsync();
            BarcodeTextBox.Focus();
        }
        else if (e.Key == Key.F4)
        {
            vm.RemoveSelectedItem();
            BarcodeTextBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelSale();
            BarcodeTextBox.Focus();
        }
    }
}
