using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class ProductsViewModel : INotifyPropertyChanged
{
    private readonly IProductCacheRepository _repository;
    private string _query = string.Empty;
    private ProductItemViewModel? _selectedProduct;
    private string _statusMessage = string.Empty;

    public ProductsViewModel(IProductCacheRepository repository)
    {
        _repository = repository;
        SearchCommand = new RelayCommand(SearchAsync);
        NewCommand = new RelayCommand(New);
        SaveCommand = new RelayCommand(SaveAsync, () => SelectedProduct is not null);
        ToggleCommand = new RelayCommand(ToggleAsync, () => SelectedProduct is not null);
        _ = SearchAsync();
    }

    public ObservableCollection<ProductItemViewModel> Products { get; } = [];
    public RelayCommand SearchCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ToggleCommand { get; }

    public string Query { get => _query; set => SetField(ref _query, value); }
    public ProductItemViewModel? SelectedProduct { get => _selectedProduct; set { SetField(ref _selectedProduct, value); SaveCommand.RaiseCanExecuteChanged(); ToggleCommand.RaiseCanExecuteChanged(); } }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public async Task SearchAsync()
    {
        var data = await _repository.SearchAsync(Query);
        Products.Clear();
        foreach (var p in data)
        {
            Products.Add(new ProductItemViewModel { Id = p.ProductId, Barcode = p.Barcode, Description = p.Description, PriceInput = (p.PriceCents / 100m).ToString("F2"), Active = p.Active });
        }

        StatusMessage = $"{Products.Count} produto(s) carregado(s).";
    }

    public void New()
    {
        var item = new ProductItemViewModel { Id = Guid.NewGuid().ToString(), Barcode = string.Empty, Description = string.Empty, PriceInput = "0,00", Active = true };
        Products.Insert(0, item);
        SelectedProduct = item;
    }

    public async Task SaveAsync()
    {
        if (SelectedProduct is null || !MoneyFormatter.TryParseToCents(SelectedProduct.PriceInput, out var cents))
        {
            StatusMessage = "Preço inválido.";
            return;
        }

        var existing = await _repository.FindByIdAsync(SelectedProduct.Id);
        var entity = new ProductCacheItem
        {
            ProductId = SelectedProduct.Id,
            Barcode = SelectedProduct.Barcode.Trim(),
            Description = SelectedProduct.Description.Trim(),
            PriceCents = cents,
            Active = SelectedProduct.Active,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (existing is null) await _repository.AddAsync(entity);
        else await _repository.UpdateAsync(entity);

        StatusMessage = "Produto salvo localmente.";
        await SearchAsync();
    }

    public async Task ToggleAsync()
    {
        if (SelectedProduct is null) return;
        await _repository.ToggleActiveAsync(SelectedProduct.Id, !SelectedProduct.Active);
        StatusMessage = "Status atualizado.";
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
