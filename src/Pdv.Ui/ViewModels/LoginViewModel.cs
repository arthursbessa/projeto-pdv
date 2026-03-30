using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Repositories;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthApiClient _authApiClient;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IUserRepository _users;
    private readonly IStoreSettingsApiClient _storeSettingsApiClient;
    private readonly SessionContext _session;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Informe as credenciais do operador.";
    private bool _isBusy;
    private string _storeName = "Sua loja";
    private string _storeDocument = "CNPJ nao informado";
    private string _storeAddress = "Endereco nao informado";
    private string _storeLogoPath = string.Empty;

    public LoginViewModel(
        IAuthApiClient authApiClient,
        ICashRegisterRepository cashRegisters,
        IUserRepository users,
        IStoreSettingsApiClient storeSettingsApiClient,
        SessionContext session,
        IStoreSettingsRepository storeSettingsRepository,
        IErrorFileLogger errorLogger)
    {
        _authApiClient = authApiClient;
        _cashRegisters = cashRegisters;
        _users = users;
        _storeSettingsApiClient = storeSettingsApiClient;
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
    public string StoreLogoPath { get => _storeLogoPath; private set => SetField(ref _storeLogoPath, value); }

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
                StatusMessage = "Usuario ou senha invalidos.";
                return false;
            }

            await CacheAuthenticatedUserAsync(user, Password);
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
                StatusMessage = "Nao foi possivel entrar agora. Verifique usuario, senha e conexao.";
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
            var localStore = await _storeSettingsRepository.GetCurrentAsync();
            ApplyStoreInfo(localStore);

            var remoteStore = await _storeSettingsApiClient.GetSettingsAsync();
            if (remoteStore is null)
            {
                return;
            }

            await _storeSettingsRepository.UpsertAsync(remoteStore);
            ApplyStoreInfo(remoteStore);
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao carregar informacoes da loja na tela de login", ex);
        }
    }

    private void ApplyStoreInfo(StoreSettings? store)
    {
        if (store is null)
        {
            return;
        }

        StoreName = string.IsNullOrWhiteSpace(store.StoreName) ? "Sua loja" : store.StoreName;
        StoreDocument = string.IsNullOrWhiteSpace(store.Cnpj) ? "CNPJ nao informado" : $"CNPJ: {store.Cnpj}";
        StoreAddress = string.IsNullOrWhiteSpace(store.Address) ? "Endereco nao informado" : store.Address;
        StoreLogoPath = store.LogoLocalPath;
    }

    private async Task CacheAuthenticatedUserAsync(UserAccount remoteUser, string password)
    {
        var existing = await _users.FindByIdAsync(remoteUser.Id);
        var cachedUser = new UserAccount
        {
            Id = remoteUser.Id,
            Username = remoteUser.Username,
            FullName = remoteUser.FullName,
            PasswordHash = UserRepository.HashPassword(password),
            Active = remoteUser.Active,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (existing is null)
        {
            await _users.AddAsync(cachedUser);
        }
        else
        {
            await _users.UpdateAsync(cachedUser);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
