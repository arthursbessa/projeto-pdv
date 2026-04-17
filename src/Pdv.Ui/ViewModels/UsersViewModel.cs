using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Repositories;
using Pdv.Application.Utilities;

namespace Pdv.Ui.ViewModels;

public sealed class UsersViewModel : INotifyPropertyChanged
{
    private readonly IUserRepository _repository;
    private string _query = string.Empty;
    private UserItemViewModel? _selectedUser;
    private string _statusMessage = string.Empty;

    public UsersViewModel(IUserRepository repository)
    {
        _repository = repository;
        SearchCommand = new RelayCommand(SearchAsync);
        NewCommand = new RelayCommand(New);
        SaveCommand = new RelayCommand(SaveAsync, () => SelectedUser is not null);
        ToggleCommand = new RelayCommand(ToggleAsync, () => SelectedUser is not null);
        _ = SearchAsync();
    }

    public ObservableCollection<UserItemViewModel> Users { get; } = [];
    public RelayCommand SearchCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public string Query { get => _query; set => SetField(ref _query, value); }
    public UserItemViewModel? SelectedUser { get => _selectedUser; set { SetField(ref _selectedUser, value); SaveCommand.RaiseCanExecuteChanged(); ToggleCommand.RaiseCanExecuteChanged(); } }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public async Task SearchAsync()
    {
        var users = await _repository.SearchAsync(TextNormalization.TrimToEmpty(Query));
        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(new UserItemViewModel { Id = user.Id, Username = user.Username, FullName = user.FullName, Active = user.Active });
        }
        StatusMessage = $"{Users.Count} usuário(s) carregado(s).";
    }

    public void New()
    {
        var item = new UserItemViewModel { Username = string.Empty, FullName = string.Empty, Password = "123456", Active = true };
        Users.Insert(0, item);
        SelectedUser = item;
    }

    public async Task SaveAsync()
    {
        if (SelectedUser is null || string.IsNullOrWhiteSpace(SelectedUser.Username) || string.IsNullOrWhiteSpace(SelectedUser.FullName))
        {
            StatusMessage = "Preencha usuário e nome.";
            return;
        }

        var existing = await _repository.FindByIdAsync(SelectedUser.Id);
        var passwordHash = existing?.PasswordHash ?? UserRepository.HashPassword(string.IsNullOrWhiteSpace(SelectedUser.Password) ? "123456" : SelectedUser.Password);
        if (existing is not null && !string.IsNullOrWhiteSpace(SelectedUser.Password))
        {
            passwordHash = UserRepository.HashPassword(SelectedUser.Password);
        }

        var user = new UserAccount
        {
            Id = SelectedUser.Id,
            Username = TextNormalization.TrimToEmpty(SelectedUser.Username),
            FullName = TextNormalization.TrimToEmpty(SelectedUser.FullName),
            PasswordHash = passwordHash,
            Active = SelectedUser.Active,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (existing is null) await _repository.AddAsync(user);
        else await _repository.UpdateAsync(user);

        StatusMessage = "Usuário salvo.";
        await SearchAsync();
    }

    public async Task ToggleAsync()
    {
        if (SelectedUser is null) return;
        await _repository.ToggleActiveAsync(SelectedUser.Id, !SelectedUser.Active);
        await SearchAsync();
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
