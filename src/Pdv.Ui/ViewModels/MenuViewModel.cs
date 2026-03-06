using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Services;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class MenuViewModel : INotifyPropertyChanged
{
    private readonly SessionContext _session;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly DataIntegrationService _dataIntegrationService;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _statusMessage = "Gerencie seu caixa e módulos.";
    private bool _isBusy;
    private string _storeName = "Minha Loja";
    private string _storeLogoPath = string.Empty;
    private int _lastClosedCashBalanceCents;
    private int _currentCashBalanceCents;

    public MenuViewModel(SessionContext session, ICashRegisterRepository cashRegisters, DataIntegrationService dataIntegrationService, IStoreSettingsRepository storeSettingsRepository, IErrorFileLogger errorLogger)
    {
        _session = session;
        _cashRegisters = cashRegisters;
        _dataIntegrationService = dataIntegrationService;
        _storeSettingsRepository = storeSettingsRepository;
        _errorLogger = errorLogger;
    }

    public string Username => _session.CurrentUser?.FullName ?? "-";
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
    public string StoreName { get => _storeName; set => SetField(ref _storeName, value); }
    public string StoreLogoPath { get => _storeLogoPath; set => SetField(ref _storeLogoPath, value); }
    public int LastClosedCashBalanceCents { get => _lastClosedCashBalanceCents; private set => SetField(ref _lastClosedCashBalanceCents, value); }
    public string LastClosedCashBalance => MoneyFormatter.FormatFromCents(LastClosedCashBalanceCents);
    public int CurrentCashBalanceCents { get => _currentCashBalanceCents; private set => SetField(ref _currentCashBalanceCents, value); }
    public string CurrentCashBalance => MoneyFormatter.FormatFromCents(CurrentCashBalanceCents);
    public string CashStatus => _session.OpenCashRegister is null ? "Caixa fechado" : $"Caixa aberto em {_session.OpenCashRegister.BusinessDate}";

    public async Task LoadAsync()
    {
        _session.OpenCashRegister = await _cashRegisters.GetOpenSessionAsync();
        var lastClosedSession = await _cashRegisters.GetLastClosedSessionAsync();
        LastClosedCashBalanceCents = lastClosedSession?.ClosingAmountCents ?? 0;
        await RefreshCashStatusAsync();

        var settings = await _storeSettingsRepository.GetCurrentAsync();
        if (settings is not null)
        {
            StoreName = settings.StoreName;
            StoreLogoPath = settings.LogoLocalPath;
        }

        OnPropertyChanged(nameof(CashStatus));
        OnPropertyChanged(nameof(LastClosedCashBalance));
        OnPropertyChanged(nameof(CurrentCashBalance));
    }

    public async Task IntegratePendingSalesAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            StatusMessage = "Integrando vendas, usuários, produtos e configurações...";
            var result = await _dataIntegrationService.IntegrateAllAsync();
            var settings = await _storeSettingsRepository.GetCurrentAsync();
            if (settings is not null)
            {
                StoreName = settings.StoreName;
                StoreLogoPath = settings.LogoLocalPath;
            }

            StatusMessage = $"Integração concluída. Vendas: {result.SentSales} enviadas ({result.PendingSales} pendentes). Produtos sincronizados: {result.SyncedProducts}. Usuários sincronizados: {result.SyncedUsers}. Configurações: {(result.StoreSettingsSynced ? "ok" : "sem atualização")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao integrar dados: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> OpenCashRegisterAsync(string openAmount)
    {
        if (IsBusy) return false;

        if (_session.CurrentUser is null)
        {
            StatusMessage = "Sessão de usuário inválida.";
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(openAmount, out var amount))
        {
            StatusMessage = "Valor de abertura inválido.";
            return false;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Abrindo caixa...";
            _session.OpenCashRegister = await _cashRegisters.OpenAsync(amount, _session.CurrentUser.Id, DateTimeOffset.Now);
            await RefreshCashStatusAsync();
            StatusMessage = "Caixa aberto com sucesso.";
            OnPropertyChanged(nameof(CashStatus));
            OnPropertyChanged(nameof(CurrentCashBalance));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> CloseCashRegisterAsync()
    {
        if (IsBusy) return false;

        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Não há caixa aberto para encerramento.";
            return false;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Encerrando caixa...";
            await _cashRegisters.CloseAsync(_session.OpenCashRegister.Id, _session.CurrentUser.Id, DateTimeOffset.Now);
            var lastClosedSession = await _cashRegisters.GetLastClosedSessionAsync();
            LastClosedCashBalanceCents = lastClosedSession?.ClosingAmountCents ?? 0;
            await RefreshCashStatusAsync();
            _session.OpenCashRegister = null;
            CurrentCashBalanceCents = 0;
            StatusMessage = "Caixa encerrado com sucesso.";
            OnPropertyChanged(nameof(CashStatus));
            OnPropertyChanged(nameof(LastClosedCashBalance));
            OnPropertyChanged(nameof(CurrentCashBalance));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RegisterWithdrawalAsync(string amount, string reason)
    {
        if (IsBusy) return false;

        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Não há caixa aberto para sangria.";
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(amount, out var amountCents) || amountCents <= 0)
        {
            StatusMessage = "Valor de sangria inválido.";
            return false;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Registrando sangria...";
            await _cashRegisters.RegisterWithdrawalAsync(_session.OpenCashRegister.Id, amountCents, reason, _session.CurrentUser.Id, DateTimeOffset.Now);
            await RefreshCashStatusAsync();
            StatusMessage = "Sangria registrada com sucesso.";
            OnPropertyChanged(nameof(CurrentCashBalance));
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao registrar sangria", ex);
            StatusMessage = $"Falha ao registrar sangria: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }


    public async Task RefreshCashStatusAsync()
    {
        var snapshot = await _cashRegisters.GetCashStatusSnapshotAsync(DateTimeOffset.Now);
        CurrentCashBalanceCents = snapshot.CurrentBalanceCents;
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
