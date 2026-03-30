using System.Windows;
using System.Windows.Input;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class ProductLookupWindow : Window
{
    public ProductLookupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ProductLookupViewModel vm)
            {
                await vm.LoadAsync();
                QueryTextBox.Focus();
                QueryTextBox.SelectAll();
            }
        };
    }

    public ProductLookupItemViewModel? SelectedProduct { get; private set; }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void ProductsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void ConfirmSelection()
    {
        if (DataContext is not ProductLookupViewModel vm || vm.SelectedProduct is null)
        {
            return;
        }

        SelectedProduct = vm.SelectedProduct;
        DialogResult = true;
        Close();
    }
}
