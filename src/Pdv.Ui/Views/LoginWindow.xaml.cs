using System.Windows;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = PasswordInput.Password;
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm) return;

        if (await vm.LoginAsync())
        {
            DialogResult = true;
            Close();
        }
    }
}
