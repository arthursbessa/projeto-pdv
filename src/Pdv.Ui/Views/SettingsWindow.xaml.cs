using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
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
