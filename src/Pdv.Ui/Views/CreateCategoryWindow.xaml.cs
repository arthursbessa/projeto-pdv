using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class CreateCategoryWindow : Window
{
    public CreateCategoryWindow()
    {
        InitializeComponent();
    }

    public CreateCategoryViewModel? ViewModel => DataContext as CreateCategoryViewModel;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CreateCategoryViewModel vm)
        {
            vm.New();
            await vm.LoadAsync();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateCategoryViewModel vm)
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
