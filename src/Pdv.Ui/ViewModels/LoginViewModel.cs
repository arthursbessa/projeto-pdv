using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;

namespace Pdv.Ui.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IUserRepository _users;
    private readonly SessionContext _session;
    private string _username = "admin";
    private string _password = "admin";
    private string _statusMessage = "Informe suas credenciais.";

    public LoginViewModel(IUserRepository users, SessionContext session)
    {
        _users = users;
        _session = session;
    }

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public async Task<bool> LoginAsync()
    {
        var user = await _users.AuthenticateAsync(Username, Password);
        if (user is null)
        {
            StatusMessage = "Usuário ou senha inválidos.";
            return false;
        }

        _session.CurrentUser = user;
        StatusMessage = $"Bem-vindo, {user.FullName}.";
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
