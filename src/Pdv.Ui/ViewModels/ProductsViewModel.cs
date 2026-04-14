using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class ProductsViewModel : INotifyPropertyChanged
{
    private readonly IProductCacheRepository _repository;
    private readonly IProductsApiClient _productsApiClient;
    private readonly IReferenceDataApiClient _referenceDataApiClient;
    private readonly IPdvSettingsRepository _settingsRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IErrorFileLogger _errorLogger;

    private string _productId = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _sku = string.Empty;
    private string _barcode = string.Empty;
    private string _unit = "un";
    private string? _categoryId;
    private string? _supplierId;
    private string _ncm = string.Empty;
    private string _cfop = "5102";
    private string _costPriceInput = "0,00";
    private string _salePriceInput = "0,00";
    private int _stockQuantity;
    private int _minStock;
    private bool _isActive = true;
    private bool _isEditMode;
    private bool _isBusy;
    private string _statusMessage = "Preencha os dados do produto.";
    private Brush _statusBrush = Brushes.DimGray;
    private ProductTextCaseMode _productTextCase = ProductTextCaseMode.Original;

    public ProductsViewModel(
        IProductCacheRepository repository,
        IProductsApiClient productsApiClient,
        IReferenceDataApiClient referenceDataApiClient,
        IPdvSettingsRepository settingsRepository,
        IOutboxRepository outboxRepository,
        IErrorFileLogger errorLogger)
    {
        _repository = repository;
        _productsApiClient = productsApiClient;
        _referenceDataApiClient = referenceDataApiClient;
        _settingsRepository = settingsRepository;
        _outboxRepository = outboxRepository;
        _errorLogger = errorLogger;

        SaveCommand = new RelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new RelayCommand(DeleteAsync, () => IsEditMode && !IsBusy);
    }

    public ObservableCollection<LookupOption> Categories { get; } = [];
    public ObservableCollection<LookupOption> Suppliers { get; } = [];
    public ObservableCollection<string> Units { get; } = new(["un", "kg", "cx", "lt"]);

    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetField(ref _isEditMode, value))
            {
                DeleteCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(ShowDeleteButton));
            }
        }
    }

    public string WindowTitle => IsEditMode ? "Editar Produto" : "Novo Produto";
    public string SaveButtonText => IsEditMode ? "Salvar" : "Criar";
    public bool ShowDeleteButton => IsEditMode;
    public string MarginPercent
    {
        get
        {
            var sale = MoneyFormatter.TryParseToCents(SalePriceInput, out var saleCents) ? saleCents : 0;
            var cost = MoneyFormatter.TryParseToCents(CostPriceInput, out var costCents) ? costCents : 0;
            if (cost <= 0)
            {
                return "0,0%";
            }

            var margin = ((sale - cost) / (decimal)cost) * 100m;
            return margin.ToString("0.0") + "%";
        }
    }

    public string ProductId
    {
        get => _productId;
        private set => SetField(ref _productId, value);
    }

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Sku { get => _sku; set => SetField(ref _sku, value); }
    public string Barcode { get => _barcode; set => SetField(ref _barcode, value); }
    public string Unit { get => _unit; set => SetField(ref _unit, value); }
    public string? CategoryId { get => _categoryId; set => SetField(ref _categoryId, value); }
    public string? SupplierId { get => _supplierId; set => SetField(ref _supplierId, value); }
    public string Ncm { get => _ncm; set => SetField(ref _ncm, value); }
    public string Cfop { get => _cfop; set => SetField(ref _cfop, value); }
    public string CostPriceInput
    {
        get => _costPriceInput;
        set
        {
            if (SetField(ref _costPriceInput, value))
            {
                OnPropertyChanged(nameof(MarginPercent));
            }
        }
    }

    public string SalePriceInput
    {
        get => _salePriceInput;
        set
        {
            if (SetField(ref _salePriceInput, value))
            {
                OnPropertyChanged(nameof(MarginPercent));
            }
        }
    }
    public int StockQuantity { get => _stockQuantity; set => SetField(ref _stockQuantity, value); }
    public int MinStock { get => _minStock; set => SetField(ref _minStock, value); }
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetField(ref _statusBrush, value); }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await LoadSettingsAsync();
            Categories.Clear();
            Suppliers.Clear();

            ReferenceDataSnapshot referenceData;
            try
            {
                referenceData = await _referenceDataApiClient.GetReferenceDataAsync();
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao carregar dados de referencia do produto no PDV", ex);
                StatusMessage = "Nao foi possivel carregar categorias e fornecedores.";
                StatusBrush = Brushes.Firebrick;
                referenceData = new ReferenceDataSnapshot();
            }

            Categories.Add(new LookupOption { Id = string.Empty, Name = "Nenhuma" });
            foreach (var category in referenceData.Categories.OrderBy(c => c.Name))
            {
                Categories.Add(category);
            }

            Suppliers.Add(new LookupOption { Id = string.Empty, Name = "Nenhum" });
            foreach (var supplier in referenceData.Suppliers.OrderBy(s => s.Name))
            {
                Suppliers.Add(supplier);
            }

            if (string.IsNullOrWhiteSpace(Sku))
            {
                Sku = GenerateSku();
            }

            StatusMessage = IsEditMode ? "Edite os dados do produto." : "Preencha os dados do produto.";
            StatusBrush = Brushes.DimGray;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void New()
    {
        ProductId = Guid.NewGuid().ToString();
        Name = string.Empty;
        Sku = GenerateSku();
        Barcode = string.Empty;
        Unit = "un";
        CategoryId = string.Empty;
        SupplierId = string.Empty;
        Ncm = string.Empty;
        Cfop = "5102";
        CostPriceInput = "0,00";
        SalePriceInput = "0,00";
        StockQuantity = 0;
        MinStock = 0;
        IsActive = true;
        IsEditMode = false;
        StatusMessage = "Preencha os dados do produto.";
        StatusBrush = Brushes.DimGray;
    }

    public async Task OpenExistingAsync(string productId)
    {
        await LoadSettingsAsync();
        var existing = await _repository.FindByIdAsync(productId);
        if (existing is null)
        {
            New();
            return;
        }

        ProductId = existing.ProductId;
        Name = ProductTextFormatter.Format(existing.Description, _productTextCase);
        Sku = existing.Sku;
        Barcode = existing.Barcode;
        Unit = "un";
        CategoryId = existing.CategoryId ?? string.Empty;
        SupplierId = existing.SupplierId ?? string.Empty;
        Ncm = existing.Ncm ?? string.Empty;
        Cfop = string.IsNullOrWhiteSpace(existing.Cfop) ? "5102" : existing.Cfop;
        CostPriceInput = MoneyFormatter.FormatFromCents(existing.CostPriceCents > 0 ? existing.CostPriceCents : existing.PriceCents);
        SalePriceInput = MoneyFormatter.FormatFromCents(existing.PriceCents);
        StockQuantity = 0;
        MinStock = 0;
        IsActive = existing.Active;
        IsEditMode = true;
        StatusMessage = "Edite os dados do produto.";
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
            StatusMessage = "Nome do produto e obrigatorio.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(SalePriceInput, out var saleCents))
        {
            StatusMessage = "Preco de venda invalido.";
            StatusBrush = Brushes.Firebrick;
            return false;
        }

        var costCents = MoneyFormatter.TryParseToCents(CostPriceInput, out var parsedCost)
            ? parsedCost
            : saleCents;

        var entity = new ProductCacheItem
        {
            ProductId = ProductId,
            Sku = string.IsNullOrWhiteSpace(Sku) ? GenerateSku() : Sku.Trim(),
            Barcode = Barcode.Trim(),
            Description = Name.Trim(),
            CategoryId = string.IsNullOrWhiteSpace(CategoryId) ? null : CategoryId,
            SupplierId = string.IsNullOrWhiteSpace(SupplierId) ? null : SupplierId,
            Ncm = string.IsNullOrWhiteSpace(Ncm) ? null : Ncm.Trim(),
            Cfop = string.IsNullOrWhiteSpace(Cfop) ? "5102" : Cfop.Trim(),
            PriceCents = saleCents,
            Active = IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        IsBusy = true;
        try
        {
            var existing = await _repository.FindByIdAsync(entity.ProductId);
            entity.CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow;

            if (existing is null)
            {
                await _repository.AddAsync(entity);
            }
            else
            {
                await _repository.UpdateAsync(entity);
            }

            var adminItem = BuildAdminItem(entity, costCents);
            try
            {
                if (existing is null)
                {
                    await _productsApiClient.CreateAsync(adminItem);
                }
                else
                {
                    await _productsApiClient.UpdateAsync(adminItem);
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao sincronizar cadastro de produto do PDV", ex);
                await QueueProductSyncAsync(entity, costCents, existing is null);
            }

            StatusMessage = "Produto salvo com sucesso.";
            StatusBrush = Brushes.SeaGreen;
            IsEditMode = true;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> DeleteAsync()
    {
        if (!IsEditMode || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await _repository.DeleteAsync(ProductId);

            try
            {
                await _productsApiClient.DeleteAsync(ProductId);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao excluir produto no PDV", ex);
            }

            StatusMessage = "Produto excluido.";
            StatusBrush = Brushes.SeaGreen;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ProductAdminItem BuildAdminItem(ProductCacheItem entity, int costCents) => new()
    {
        Id = entity.ProductId,
        Name = entity.Description.Trim(),
        Sku = string.IsNullOrWhiteSpace(entity.Sku) ? entity.ProductId : entity.Sku.Trim(),
        Barcode = entity.Barcode.Trim(),
        CategoryId = entity.CategoryId,
        SupplierId = entity.SupplierId,
        Ncm = string.IsNullOrWhiteSpace(Ncm) ? null : Ncm.Trim(),
        Cfop = string.IsNullOrWhiteSpace(Cfop) ? "5102" : Cfop.Trim(),
        PriceCents = entity.PriceCents,
        CostPriceCents = costCents,
        StockQuantity = StockQuantity,
        MinStock = MinStock,
        Unit = string.IsNullOrWhiteSpace(Unit) ? "un" : Unit.Trim(),
        Active = entity.Active
    };

    private async Task QueueProductSyncAsync(ProductCacheItem entity, int costCents, bool isNew)
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
                category_id = string.IsNullOrWhiteSpace(entity.CategoryId) ? null : entity.CategoryId,
                supplier_id = string.IsNullOrWhiteSpace(entity.SupplierId) ? null : entity.SupplierId,
                ncm = string.IsNullOrWhiteSpace(entity.Ncm) ? null : entity.Ncm.Trim(),
                cfop = string.IsNullOrWhiteSpace(entity.Cfop) ? "5102" : entity.Cfop.Trim(),
                sale_price = entity.PriceCents / 100m,
                cost_price = costCents / 100m,
                stock_quantity = StockQuantity,
                min_stock = MinStock,
                unit = string.IsNullOrWhiteSpace(Unit) ? "un" : Unit.Trim(),
                is_active = entity.Active
            }
        });

        await _outboxRepository.EnqueueAsync("ProductUpserted", payload);
    }

    private string GenerateSku()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsRepository.GetCurrentAsync();
        _productTextCase = settings.ProductTextCase;
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
