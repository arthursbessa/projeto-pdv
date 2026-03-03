using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;

namespace Pdv.Ui.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthApiClient _authApiClient;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly SessionContext _session;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Informe as credenciais do Lovable.";
    private bool _isBusy;

    public LoginViewModel(IAuthApiClient authApiClient, ICashRegisterRepository cashRegisters, SessionContext session)
    {
        _authApiClient = authApiClient;
        _cashRegisters = cashRegisters;
        _session = session;
    }

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    public async Task<bool> LoginAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Autenticando operador...";
            var user = await _authApiClient.AuthenticateAsync(Username, Password);
            if (user is null)
            {
                StatusMessage = "Usuário ou senha inválidos.";
                return false;
            }

            _session.CurrentUser = user;

            var openSession = await _cashRegisters.GetOpenSessionAsync();
            if (openSession is not null && openSession.BusinessDate != DateTimeOffset.Now.ToString("yyyy-MM-dd"))
            {
                await _cashRegisters.CloseAsync(openSession.Id, user.Id, DateTimeOffset.Now);
                _session.OpenCashRegister = null;
                StatusMessage = $"Bem-vindo, {user.FullName}. Caixa antigo fechado automaticamente.";
                return true;
            }

            _session.OpenCashRegister = openSession;
            StatusMessage = $"Bem-vindo, {user.FullName}.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha no login remoto: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
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
