using System.Collections.ObjectModel;
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
    private readonly IOutboxRepository _outboxRepository;
    private readonly SyncService _syncService;
    private readonly IErrorFileLogger _errorLogger;
    private string _statusMessage = "Gerencie seu caixa e modulos.";
    private bool _isBusy;
    private string _storeName = "Minha Loja";
    private string _storeLogoPath = string.Empty;
    private int _lastClosedCashBalanceCents;
    private int _currentCashBalanceCents;

    public MenuViewModel(
        SessionContext session,
        ICashRegisterRepository cashRegisters,
        DataIntegrationService dataIntegrationService,
        IStoreSettingsRepository storeSettingsRepository,
        IOutboxRepository outboxRepository,
        SyncService syncService,
        IErrorFileLogger errorLogger)
    {
        _session = session;
        _cashRegisters = cashRegisters;
        _dataIntegrationService = dataIntegrationService;
        _storeSettingsRepository = storeSettingsRepository;
        _outboxRepository = outboxRepository;
        _syncService = syncService;
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
    public ObservableCollection<IntegrationStatusItemViewModel> IntegrationStatuses { get; } = [];

    public async Task LoadAsync()
    {
        _session.OpenCashRegister = await _cashRegisters.GetOpenSessionAsync();
        var lastClosedSession = await _cashRegisters.GetLastClosedSessionAsync();
        LastClosedCashBalanceCents = lastClosedSession?.ClosingAmountCents ?? 0;
        await RefreshCashStatusAsync();
        await RefreshIntegrationStatusesAsync();

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

    public async Task RefreshIntegrationStatusesAsync()
    {
        var pendingByType = await _outboxRepository.GetPendingCountsByTypeAsync();
        IntegrationStatuses.Clear();
        IntegrationStatuses.Add(BuildStatus("Abertura de caixa", "CashRegisterOpened", pendingByType));
        IntegrationStatuses.Add(BuildStatus("Encerramento de caixa", "CashRegisterClosed", pendingByType));
        IntegrationStatuses.Add(BuildStatus("Sangria", "CashWithdrawalCreated", pendingByType));
        IntegrationStatuses.Add(BuildStatus("Vendas", "SaleCreated", pendingByType));
    }

    public async Task IntegratePendingSalesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Integrando dados pendentes manualmente...";
            var result = await _dataIntegrationService.IntegrateAllAsync();
            var settings = await _storeSettingsRepository.GetCurrentAsync();
            if (settings is not null)
            {
                StoreName = settings.StoreName;
                StoreLogoPath = settings.LogoLocalPath;
            }

            await RefreshIntegrationStatusesAsync();
            StatusMessage = $"Integracao concluida. Enviadas: {result.SentSales}. Pendentes: {result.PendingSales}.";
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha na integracao manual de dados", ex);
            StatusMessage = "Nao foi possivel integrar os dados agora. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> OpenCashRegisterAsync(string openAmount)
    {
        if (_session.CurrentUser is null)
        {
            StatusMessage = "Sessao de usuario invalida.";
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(openAmount, out var amount))
        {
            StatusMessage = "Valor de abertura invalido.";
            return false;
        }

        try
        {
            StatusMessage = "Abrindo caixa localmente e integrando em segundo plano...";
            _session.OpenCashRegister = await _cashRegisters.OpenAsync(amount, _session.CurrentUser.Id, DateTimeOffset.Now);
            await RefreshCashStatusAsync();
            OnPropertyChanged(nameof(CashStatus));
            OnPropertyChanged(nameof(CurrentCashBalance));
            await RefreshIntegrationStatusesAsync();
            TriggerBackgroundIntegration();
            StatusMessage = "Caixa aberto com sucesso. Integracao assincrona iniciada.";
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao abrir caixa", ex);
            StatusMessage = "Nao foi possivel abrir o caixa. Confira os dados e tente novamente.";
            return false;
        }
    }

    public async Task<bool> CloseCashRegisterAsync()
    {
        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Nao ha caixa aberto para encerramento.";
            return false;
        }

        try
        {
            StatusMessage = "Encerrando caixa localmente e integrando em segundo plano...";
            await _cashRegisters.CloseAsync(_session.OpenCashRegister.Id, _session.CurrentUser.Id, DateTimeOffset.Now);
            var lastClosedSession = await _cashRegisters.GetLastClosedSessionAsync();
            LastClosedCashBalanceCents = lastClosedSession?.ClosingAmountCents ?? 0;
            await RefreshCashStatusAsync();
            _session.OpenCashRegister = null;
            CurrentCashBalanceCents = 0;
            await RefreshIntegrationStatusesAsync();
            TriggerBackgroundIntegration();
            StatusMessage = "Caixa encerrado com sucesso. Integracao assincrona iniciada.";
            OnPropertyChanged(nameof(CashStatus));
            OnPropertyChanged(nameof(LastClosedCashBalance));
            OnPropertyChanged(nameof(CurrentCashBalance));
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao encerrar caixa", ex);
            StatusMessage = "Nao foi possivel encerrar o caixa agora. Tente novamente.";
            return false;
        }
    }

    public async Task<bool> RegisterWithdrawalAsync(string amount, string reason)
    {
        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Nao ha caixa aberto para sangria.";
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(amount, out var amountCents) || amountCents <= 0)
        {
            StatusMessage = "Valor de sangria invalido.";
            return false;
        }

        try
        {
            StatusMessage = "Registrando sangria localmente e integrando em segundo plano...";
            await _cashRegisters.RegisterWithdrawalAsync(_session.OpenCashRegister.Id, amountCents, reason, _session.CurrentUser.Id, DateTimeOffset.Now);
            await RefreshCashStatusAsync();
            await RefreshIntegrationStatusesAsync();
            TriggerBackgroundIntegration();
            StatusMessage = "Sangria registrada com sucesso. Integracao assincrona iniciada.";
            OnPropertyChanged(nameof(CurrentCashBalance));
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao registrar sangria", ex);
            StatusMessage = "Nao foi possivel registrar a sangria agora. Tente novamente.";
            return false;
        }
    }

    public async Task RefreshCashStatusAsync()
    {
        var snapshot = await _cashRegisters.GetCashStatusSnapshotAsync(DateTimeOffset.Now);
        CurrentCashBalanceCents = snapshot.CurrentBalanceCents;
    }

    private IntegrationStatusItemViewModel BuildStatus(string name, string eventType, IReadOnlyDictionary<string, int> pendingByType)
    {
        pendingByType.TryGetValue(eventType, out var pendingCount);
        return new IntegrationStatusItemViewModel
        {
            Name = name,
            IsIntegrated = pendingCount == 0,
            PendingCount = pendingCount
        };
    }

    private void TriggerBackgroundIntegration()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _syncService.RunOnceAsync();
                await App.Current.Dispatcher.InvokeAsync(async () => await RefreshIntegrationStatusesAsync());
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha na integracao assincrona", ex);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
