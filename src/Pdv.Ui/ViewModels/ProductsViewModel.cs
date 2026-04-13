using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class ProductsViewModel : INotifyPropertyChanged
{
    private readonly IProductCacheRepository _repository;
    private readonly IProductsApiClient _productsApiClient;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _query = string.Empty;
    private ProductItemViewModel? _selectedProduct;
    private string _statusMessage = string.Empty;

    public ProductsViewModel(
        IProductCacheRepository repository,
        IProductsApiClient productsApiClient,
        IOutboxRepository outboxRepository,
        IErrorFileLogger errorLogger)
    {
        _repository = repository;
        _productsApiClient = productsApiClient;
        _outboxRepository = outboxRepository;
        _errorLogger = errorLogger;
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

    public ProductItemViewModel? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetField(ref _selectedProduct, value);
            SaveCommand.RaiseCanExecuteChanged();
            ToggleCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public async Task SearchAsync()
    {
        var data = await _repository.SearchAsync(Query);
        Products.Clear();
        foreach (var p in data)
        {
            Products.Add(new ProductItemViewModel
            {
                Id = p.ProductId,
                Sku = string.IsNullOrWhiteSpace(p.Sku)
                    ? (p.ProductId.Length >= 6 ? p.ProductId[..6].ToUpperInvariant() : p.ProductId.ToUpperInvariant())
                    : p.Sku,
                Barcode = p.Barcode,
                Description = p.Description,
                PriceInput = (p.PriceCents / 100m).ToString("F2"),
                Active = p.Active
            });
        }

        StatusMessage = $"{Products.Count} produto(s) carregado(s).";
    }

    public async Task OpenExistingAsync(string productId)
    {
        await SearchAsync();
        SelectedProduct = Products.FirstOrDefault(x => x.Id == productId);
    }

    public void New()
    {
        var item = new ProductItemViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Sku = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Barcode = string.Empty,
            Description = string.Empty,
            PriceInput = "0,00",
            Active = true
        };
        Products.Insert(0, item);
        SelectedProduct = item;
    }

    public async Task SaveAsync()
    {
        if (SelectedProduct is null || !MoneyFormatter.TryParseToCents(SelectedProduct.PriceInput, out var cents))
        {
            StatusMessage = "Preco invalido.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedProduct.Description) || string.IsNullOrWhiteSpace(SelectedProduct.Sku))
        {
            StatusMessage = "Descricao e SKU sao obrigatorios.";
            return;
        }

        var existing = await _repository.FindByIdAsync(SelectedProduct.Id);
        var entity = new ProductCacheItem
        {
            ProductId = SelectedProduct.Id,
            Sku = SelectedProduct.Sku.Trim(),
            Barcode = SelectedProduct.Barcode.Trim(),
            Description = SelectedProduct.Description.Trim(),
            PriceCents = cents,
            Active = SelectedProduct.Active,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (existing is null)
        {
            await _repository.AddAsync(entity);
        }
        else
        {
            await _repository.UpdateAsync(entity);
        }

        try
        {
            await SyncProductAsync(entity, existing is null);
            StatusMessage = "Produto salvo com sucesso.";
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao sincronizar cadastro de produto do PDV", ex);
            await QueueProductSyncAsync(entity, existing is null);
            StatusMessage = "Produto salvo localmente e enfileirado para sincronizacao.";
        }

        await SearchAsync();
    }

    public async Task ToggleAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        var nextActive = !SelectedProduct.Active;
        await _repository.ToggleActiveAsync(SelectedProduct.Id, nextActive);
        SelectedProduct.Active = nextActive;

        var existing = await _repository.FindByIdAsync(SelectedProduct.Id);
        if (existing is not null)
        {
            var updated = new ProductCacheItem
            {
                ProductId = existing.ProductId,
                Sku = existing.Sku,
                Barcode = existing.Barcode,
                Description = existing.Description,
                PriceCents = existing.PriceCents,
                Active = nextActive,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                await SyncProductAsync(updated, false);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao sincronizar status do produto do PDV", ex);
                await QueueProductSyncAsync(updated, false);
            }
        }

        StatusMessage = "Status atualizado.";
        await SearchAsync();
    }

    private ProductAdminItem BuildAdminItem(ProductCacheItem entity) => new()
    {
        Id = entity.ProductId,
        Name = entity.Description.Trim(),
        Sku = string.IsNullOrWhiteSpace(entity.Sku)
            ? entity.ProductId
            : entity.Sku.Trim(),
        Barcode = entity.Barcode.Trim(),
        PriceCents = entity.PriceCents,
        CostPriceCents = entity.PriceCents,
        StockQuantity = 0,
        MinStock = 0,
        Unit = "un",
        Active = entity.Active
    };

    private async Task SyncProductAsync(ProductCacheItem entity, bool isNew)
    {
        var adminItem = BuildAdminItem(entity);
        if (isNew)
        {
            await _productsApiClient.CreateAsync(adminItem);
            return;
        }

        await _productsApiClient.UpdateAsync(adminItem);
    }

    private async Task QueueProductSyncAsync(ProductCacheItem entity, bool isNew)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            is_new = isNew,
            product = new
            {
                id = entity.ProductId,
                name = entity.Description.Trim(),
                sku = string.IsNullOrWhiteSpace(entity.Sku) ? entity.ProductId : entity.Sku.Trim(),
                barcode = string.IsNullOrWhiteSpace(entity.Barcode) ? null : entity.Barcode.Trim(),
                sale_price = entity.PriceCents / 100m,
                cost_price = entity.PriceCents / 100m,
                stock_quantity = 0,
                min_stock = 0,
                unit = "un",
                is_active = entity.Active
            }
        });

        await _outboxRepository.EnqueueAsync("ProductUpserted", payload);
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
