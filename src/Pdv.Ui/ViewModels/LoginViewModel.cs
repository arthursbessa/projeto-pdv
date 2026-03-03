using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Services;

namespace Pdv.Ui.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthApiClient _authApiClient;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IUserRepository _users;
    private readonly SessionContext _session;
    private readonly DataIntegrationService _dataIntegrationService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Informe as credenciais do Lovable.";
    private bool _isBusy;

    public LoginViewModel(
        IAuthApiClient authApiClient,
        ICashRegisterRepository cashRegisters,
        IUserRepository users,
        SessionContext session,
        DataIntegrationService dataIntegrationService)
    {
        _authApiClient = authApiClient;
        _cashRegisters = cashRegisters;
        _users = users;
        _session = session;
        _dataIntegrationService = dataIntegrationService;
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
            var offlineLogin = false;
            if (user is null)
            {
                user = await _users.AuthenticateAsync(Username, Password);
                offlineLogin = user is not null;
            }

            if (user is null)
            {
                StatusMessage = "Usuário ou senha inválidos.";
                return false;
            }

            _session.CurrentUser = user;

            if (!offlineLogin)
            {
                StatusMessage = "Login confirmado. Sincronizando dados iniciais...";
                var integration = await _dataIntegrationService.IntegrateAllAsync();
                StatusMessage = $"Sincronização inicial concluída. Produtos: {integration.SyncedProducts}, usuários: {integration.SyncedUsers}, vendas enviadas: {integration.SentSales}.";
            }

            var openSession = await _cashRegisters.GetOpenSessionAsync();
            if (openSession is not null && openSession.BusinessDate != DateTimeOffset.Now.ToString("yyyy-MM-dd"))
            {
                await _cashRegisters.CloseAsync(openSession.Id, user.Id, DateTimeOffset.Now);
                _session.OpenCashRegister = null;
                StatusMessage = $"Bem-vindo, {user.FullName}. Caixa antigo fechado automaticamente.";
                return true;
            }

            _session.OpenCashRegister = openSession;
            if (offlineLogin)
            {
                StatusMessage = $"Bem-vindo, {user.FullName}. Login local (sem internet).";
            }
            else
            {
                StatusMessage = $"Bem-vindo, {user.FullName}. Sincronização inicial concluída.";
            }

            return true;
        }
        catch (Exception ex)
        {
            var localUser = await _users.AuthenticateAsync(Username, Password);
            if (localUser is null)
            {
                StatusMessage = $"Falha no login remoto e local: {ex.Message}";
                return false;
            }

            _session.CurrentUser = localUser;
            _session.OpenCashRegister = await _cashRegisters.GetOpenSessionAsync();
            StatusMessage = $"Bem-vindo, {localUser.FullName}. Login local (sem internet).";
            return true;
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
