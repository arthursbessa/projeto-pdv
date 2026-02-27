using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Domain;

namespace Pdv.Ui.ViewModels;

public sealed class SessionContext : INotifyPropertyChanged
{
    private UserAccount? _currentUser;
    private CashRegisterSession? _openCashRegister;

    public UserAccount? CurrentUser { get => _currentUser; set => SetField(ref _currentUser, value); }
    public CashRegisterSession? OpenCashRegister { get => _openCashRegister; set => SetField(ref _openCashRegister, value); }

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
