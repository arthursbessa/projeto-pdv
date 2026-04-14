using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CreateSupplierWindow : Window
{
    public CreateSupplierWindow()
    {
        InitializeComponent();
    }

    public CreateSupplierViewModel? ViewModel => DataContext as CreateSupplierViewModel;

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
