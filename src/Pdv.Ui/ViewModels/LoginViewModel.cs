using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthApiClient _authApiClient;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IUserRepository _users;
    private readonly SessionContext _session;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Informe as credenciais do Lovable.";
    private bool _isBusy;
    private string _storeName = "Sua loja";
    private string _storeDocument = "CNPJ não informado";
    private string _storeAddress = "Endereço não informado";

    public LoginViewModel(
        IAuthApiClient authApiClient,
        ICashRegisterRepository cashRegisters,
        IUserRepository users,
        SessionContext session,
        IStoreSettingsRepository storeSettingsRepository,
        IErrorFileLogger errorLogger)
    {
        _authApiClient = authApiClient;
        _cashRegisters = cashRegisters;
        _users = users;
        _session = session;
        _storeSettingsRepository = storeSettingsRepository;
        _errorLogger = errorLogger;

        _ = LoadStoreInfoAsync();
    }

    public string Username { get => _username; set => SetField(ref _username, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
    public string StoreName { get => _storeName; private set => SetField(ref _storeName, value); }
    public string StoreDocument { get => _storeDocument; private set => SetField(ref _storeDocument, value); }
    public string StoreAddress { get => _storeAddress; private set => SetField(ref _storeAddress, value); }

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
            _errorLogger.LogError("Falha no login remoto", ex);
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

    private async Task LoadStoreInfoAsync()
    {
        try
        {
            var store = await _storeSettingsRepository.GetCurrentAsync();
            if (store is null)
            {
                return;
            }

            StoreName = string.IsNullOrWhiteSpace(store.StoreName) ? "Sua loja" : store.StoreName;
            StoreDocument = string.IsNullOrWhiteSpace(store.Cnpj) ? "CNPJ não informado" : $"CNPJ: {store.Cnpj}";
            StoreAddress = string.IsNullOrWhiteSpace(store.Address) ? "Endereço não informado" : store.Address;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao carregar informações da loja na tela de login", ex);
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
