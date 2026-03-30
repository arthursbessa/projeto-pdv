using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CreateCustomerWindow : Window
{
    public CreateCustomerWindow()
    {
        InitializeComponent();
    }

    public CreateCustomerViewModel? ViewModel => DataContext as CreateCustomerViewModel;

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

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
