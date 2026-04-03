using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Ui.Services;
using Pdv.Ui.ViewModels;

namespace Pdv.Ui.Views;

public partial class LoginWindow : Window
{
    private bool _hasCheckedForUpdates;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += LoginWindow_Loaded;
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

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasCheckedForUpdates)
        {
            return;
        }

        _hasCheckedForUpdates = true;

        var updateService = App.Services.GetRequiredService<GitHubReleaseUpdateService>();
        var updateInfo = await updateService.CheckForUpdateAsync();
        if (updateInfo is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Existe uma nova versao disponivel ({updateInfo.VersionTag}). Deseja atualizar agora? O PDV sera fechado para concluir a atualizacao.",
            "Atualizacao disponivel",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (DataContext is LoginViewModel vm)
        {
            vm.StatusMessage = $"Baixando atualizacao {updateInfo.VersionTag}...";
        }

        if (!await updateService.TryStartUpdateAsync(updateInfo))
        {
            if (DataContext is LoginViewModel failedVm)
            {
                failedVm.StatusMessage = "Nao foi possivel iniciar a atualizacao agora.";
            }

            MessageBox.Show(
                this,
                "Nao foi possivel iniciar a atualizacao agora. Tente novamente em alguns instantes.",
                "Atualizacao",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Close();
        App.Current.Shutdown();
    }
}
