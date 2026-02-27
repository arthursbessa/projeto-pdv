using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class MenuViewModel : INotifyPropertyChanged
{
    private readonly SessionContext _session;
    private readonly ICashRegisterRepository _cashRegisters;
    private string _statusMessage = "Gerencie seu caixa e módulos.";
    private string _openAmount = "0,00";
    private string _closeAmount = "0,00";

    public MenuViewModel(SessionContext session, ICashRegisterRepository cashRegisters)
    {
        _session = session;
        _cashRegisters = cashRegisters;
    }

    public string Username => _session.CurrentUser?.FullName ?? "-";
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string OpenAmount { get => _openAmount; set => SetField(ref _openAmount, value); }
    public string CloseAmount { get => _closeAmount; set => SetField(ref _closeAmount, value); }
    public string CashStatus => _session.OpenCashRegister is null ? "Caixa fechado" : $"Caixa aberto em {_session.OpenCashRegister.BusinessDate}";

    public async Task LoadAsync()
    {
        _session.OpenCashRegister = await _cashRegisters.GetOpenSessionAsync();
        if (_session.OpenCashRegister is not null && _session.OpenCashRegister.BusinessDate != DateTimeOffset.Now.ToString("yyyy-MM-dd"))
        {
            StatusMessage = "ATENÇÃO: existe um caixa aberto de outro dia. Feche-o antes de abrir novo caixa.";
        }
        OnPropertyChanged(nameof(CashStatus));
    }

    public async Task OpenCashRegisterAsync()
    {
        if (_session.CurrentUser is null)
        {
            StatusMessage = "Sessão de usuário inválida.";
            return;
        }

        if (!MoneyFormatter.TryParseToCents(OpenAmount, out var amount))
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

        if (!MoneyFormatter.TryParseToCents(CloseAmount, out var amount))
        {
            StatusMessage = "Valor de fechamento inválido.";
            return;
        }

        await _cashRegisters.CloseAsync(_session.OpenCashRegister.Id, amount, _session.CurrentUser.Id, DateTimeOffset.Now);
        _session.OpenCashRegister = null;
        StatusMessage = "Caixa encerrado com sucesso.";
        OnPropertyChanged(nameof(CashStatus));
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
