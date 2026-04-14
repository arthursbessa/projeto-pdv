using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class CreateCategoryViewModel : INotifyPropertyChanged
{
    private readonly ICategoriesApiClient _categoriesApiClient;
    private readonly IErrorFileLogger _errorLogger;
    private bool _isBusy;
    private string _name = string.Empty;
    private string _statusMessage = "Preencha os dados da categoria.";
    private Brush _statusBrush = Brushes.DimGray;

    public CreateCategoryViewModel(
        ICategoriesApiClient categoriesApiClient,
        IErrorFileLogger errorLogger)
    {
        _categoriesApiClient = categoriesApiClient;
        _errorLogger = errorLogger;
    }

    public LookupOption? CreatedCategory { get; private set; }

    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }

    public async Task LoadAsync()
    {
        await Task.CompletedTask;
    }

    public void New()
    {
        Name = string.Empty;
        StatusMessage = "Preencha os dados da categoria.";
        StatusBrush = Brushes.DimGray;
    }

    public async Task<bool> SaveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Nome da categoria e obrigatorio.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }

        IsBusy = true;
        try
        {
            CreatedCategory = await _categoriesApiClient.CreateAsync(Name.Trim(), null);
            StatusMessage = "Categoria criada com sucesso.";
            StatusBrush = Brushes.SeaGreen;
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao criar categoria no PDV", ex);
            StatusMessage = "Erro ao criar categoria.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
