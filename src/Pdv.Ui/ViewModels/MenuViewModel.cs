using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Services;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class MenuViewModel : INotifyPropertyChanged
{
    private readonly SessionContext _session;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly SyncService _syncService;
    private readonly IOutboxRepository _outboxRepository;
    private string _statusMessage = "Gerencie seu caixa e módulos.";
    private bool _isBusy;

    public MenuViewModel(SessionContext session, ICashRegisterRepository cashRegisters, SyncService syncService, IOutboxRepository outboxRepository)
    {
        _session = session;
        _cashRegisters = cashRegisters;
        _syncService = syncService;
        _outboxRepository = outboxRepository;
    }

    public string Username => _session.CurrentUser?.FullName ?? "-";
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
    public string CashStatus => _session.OpenCashRegister is null ? "Caixa fechado" : $"Caixa aberto em {_session.OpenCashRegister.BusinessDate}";

    public async Task LoadAsync()
    {
        _session.OpenCashRegister = await _cashRegisters.GetOpenSessionAsync();
        OnPropertyChanged(nameof(CashStatus));
    }

    public async Task IntegratePendingSalesAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            StatusMessage = "Integrando vendas pendentes...";
            var sent = await _syncService.RunOnceAsync();
            var pending = await _outboxRepository.GetPendingCountAsync();
            StatusMessage = $"Integração concluída. Enviadas: {sent}. Pendentes: {pending}.";
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

    public async Task OpenCashRegisterAsync(string openAmount)
    {
        if (_session.CurrentUser is null)
        {
            StatusMessage = "Sessão de usuário inválida.";
            return;
        }

        if (!MoneyFormatter.TryParseToCents(openAmount, out var amount))
        {
            StatusMessage = "Valor de abertura inválido.";
            return;
        }

        try
        {
            _session.OpenCashRegister = await _cashRegisters.OpenAsync(amount, _session.CurrentUser.Id, DateTimeOffset.Now);
            StatusMessage = "Caixa aberto com sucesso.";
            OnPropertyChanged(nameof(CashStatus));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task CloseCashRegisterAsync()
    {
        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Não há caixa aberto para encerramento.";
            return;
        }

        await _cashRegisters.CloseAsync(_session.OpenCashRegister.Id, _session.CurrentUser.Id, DateTimeOffset.Now);
        _session.OpenCashRegister = null;
        StatusMessage = "Caixa encerrado com sucesso.";
        OnPropertyChanged(nameof(CashStatus));
    }

    public async Task RegisterWithdrawalAsync(string amount, string reason)
    {
        if (_session.CurrentUser is null || _session.OpenCashRegister is null)
        {
            StatusMessage = "Não há caixa aberto para sangria.";
            return;
        }

        if (!MoneyFormatter.TryParseToCents(amount, out var amountCents) || amountCents <= 0)
        {
            StatusMessage = "Valor de sangria inválido.";
            return;
        }

        await _cashRegisters.RegisterWithdrawalAsync(_session.OpenCashRegister.Id, amountCents, reason, _session.CurrentUser.Id, DateTimeOffset.Now);
        StatusMessage = "Sangria registrada com sucesso.";
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
