using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Application.Services;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SaleBuilderService _saleBuilderService;
    private readonly ISalesRepository _salesRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IProductCacheRepository _productCacheRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly SyncService _syncService;
    private readonly SessionContext _session;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private readonly AppRuntimeInfoService _runtimeInfo;
    private string _barcodeInput = string.Empty;
    private string _statusMessage = "PDV iniciado.";
    private SaleItem? _selectedItem;
    private bool _isBusy;
    private string _storeName = "LOJA";
    private string _storeCnpj = string.Empty;
    private string _storeAddress = string.Empty;
    private string _storeLogoPath = string.Empty;
    private string _lastScannedDescription = "Aguardando leitura de produto";
    private string _lastScannedBarcode = "-";
    private int _lastScannedPriceCents;
    private int _lastScannedQuantity;
    private CustomerRecord? _selectedCustomer;
    private bool _isOnlineIntegration = true;

    public MainViewModel(
        SaleBuilderService saleBuilderService,
        ISalesRepository salesRepository,
        IOutboxRepository outboxRepository,
        IProductCacheRepository productCacheRepository,
        ICustomerRepository customerRepository,
        ICatalogApiClient catalogApiClient,
        SyncService syncService,
        PdvOptions options,
        SessionContext session,
        IStoreSettingsRepository storeSettingsRepository,
        IErrorFileLogger errorLogger,
        AppRuntimeInfoService runtimeInfo)
    {
        _saleBuilderService = saleBuilderService;
        _salesRepository = salesRepository;
        _outboxRepository = outboxRepository;
        _productCacheRepository = productCacheRepository;
        _customerRepository = customerRepository;
        _catalogApiClient = catalogApiClient;
        _syncService = syncService;
        _session = session;
        _storeSettingsRepository = storeSettingsRepository;
        _errorLogger = errorLogger;
        _runtimeInfo = runtimeInfo;

        DatabaseRelativePath = $"./{options.DatabaseRelativePath.Replace('\\', '/')}";
        DatabaseFullPath = options.DatabaseFullPath;

        RemoveSelectedCommand = new RelayCommand(RemoveSelectedItem, () => SelectedItem is not null);
        CancelSaleCommand = new RelayCommand(CancelSale, () => Items.Any() || SelectedCustomer is not null);
    }

    public ObservableCollection<SaleItem> Items { get; } = [];
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand CancelSaleCommand { get; }

    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetField(ref _barcodeInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public bool IsOnlineIntegration
    {
        get => _isOnlineIntegration;
        private set
        {
            if (SetField(ref _isOnlineIntegration, value))
            {
                OnPropertyChanged(nameof(IntegrationStatusText));
                OnPropertyChanged(nameof(IntegrationStatusBrush));
            }
        }
    }

    public string IntegrationStatusText => IsOnlineIntegration ? "Integracao ONLINE" : "Integracao LOCAL";
    public Brush IntegrationStatusBrush => IsOnlineIntegration
        ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
        : new SolidColorBrush(Color.FromRgb(185, 28, 28));
    public string VersionLabel => _runtimeInfo.VersionLabel;

    public string StoreName
    {
        get => _storeName;
        set => SetField(ref _storeName, value);
    }

    public string StoreCnpj
    {
        get => _storeCnpj;
        set => SetField(ref _storeCnpj, value);
    }

    public string StoreAddress
    {
        get => _storeAddress;
        set => SetField(ref _storeAddress, value);
    }

    public string StoreLogoPath
    {
        get => _storeLogoPath;
        set => SetField(ref _storeLogoPath, value);
    }

    public string LastScannedDescription
    {
        get => _lastScannedDescription;
        private set => SetField(ref _lastScannedDescription, value);
    }

    public string LastScannedBarcode
    {
        get => _lastScannedBarcode;
        private set => SetField(ref _lastScannedBarcode, value);
    }

    public int LastScannedPriceCents
    {
        get => _lastScannedPriceCents;
        private set => SetField(ref _lastScannedPriceCents, value);
    }

    public int LastScannedQuantity
    {
        get => _lastScannedQuantity;
        private set => SetField(ref _lastScannedQuantity, value);
    }

    public CustomerRecord? SelectedCustomer
    {
        get => _selectedCustomer;
        private set
        {
            if (SetField(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(SelectedCustomerDisplay));
                OnPropertyChanged(nameof(SelectedCustomerHint));
                OnPropertyChanged(nameof(ReceiptCaption));
                CancelSaleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedCustomerDisplay => SelectedCustomer is null
        ? "Nenhum cliente selecionado"
        : string.IsNullOrWhiteSpace(SelectedCustomer.Cpf)
            ? SelectedCustomer.Name
            : $"{SelectedCustomer.Name} - {SelectedCustomer.Cpf}";

    public string SelectedCustomerHint => SelectedCustomer is null
        ? "Venda sem cliente identificado"
        : "Cliente vinculado a esta venda";

    public string LastScannedPriceFormatted => MoneyFormatter.FormatFromCents(LastScannedPriceCents);
    public string LastScannedQuantityText => LastScannedQuantity <= 0 ? "Nenhum item lido" : $"Quantidade na venda: {LastScannedQuantity}";
    public string DatabaseRelativePath { get; }
    public string DatabaseFullPath { get; }
    public string DatabaseStatus => $"Cache local: {DatabaseRelativePath}";
    public int ItemCount => Items.Sum(x => x.Quantity);
    public int TotalCents => Items.Sum(x => x.SubtotalCents);
    public string TotalFormatted => MoneyFormatter.FormatFromCents(TotalCents);

    public SaleItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetField(ref _selectedItem, value))
            {
                RemoveSelectedCommand.RaiseCanExecuteChanged();
                NotifySelectionMetrics();
            }
        }
    }

    public string SelectedItemUnitPriceFormatted => SelectedItem is null
        ? LastScannedPriceFormatted
        : MoneyFormatter.FormatFromCents(SelectedItem.PriceCents);

    public string SelectedItemQuantityFormatted => SelectedItem is null
        ? ItemCount.ToString("N0")
        : SelectedItem.Quantity.ToString("N0");

    public string DiscountFormatted => MoneyFormatter.FormatFromCents(0);
    public string ItemCountFormatted => ItemCount.ToString("N0");

    public string ReceiptCaption => SelectedCustomer is null
        ? "CUPOM FISCAL"
        : $"CUPOM FISCAL  |  {SelectedCustomer.Name.ToUpperInvariant()}";

    public async Task LoadStoreSettingsAsync()
    {
        var settings = await _storeSettingsRepository.GetCurrentAsync();
        if (settings is null)
        {
            IsOnlineIntegration = false;
            return;
        }

        StoreName = settings.StoreName;
        StoreCnpj = settings.Cnpj;
        StoreAddress = settings.Address;
        StoreLogoPath = settings.LogoLocalPath;
        IsOnlineIntegration = true;
    }

    public async Task RefreshCatalogAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Sincronizando catalogo...";
            var remoteProducts = await _catalogApiClient.GetCatalogAsync();
            foreach (var product in remoteProducts)
            {
                var existing = await _productCacheRepository.FindByIdAsync(product.ProductId);
                if (existing is null)
                {
                    await _productCacheRepository.AddAsync(product);
                }
                else
                {
                    await _productCacheRepository.UpdateAsync(product);
                }
            }

            IsOnlineIntegration = true;
            StatusMessage = $"Catalogo sincronizado: {remoteProducts.Count} item(ns).";
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao atualizar catalogo", ex);
            IsOnlineIntegration = false;
            StatusMessage = "Nao foi possivel atualizar o catalogo agora.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SyncSalesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Integrando vendas pendentes...";
            var sent = await _syncService.RunOnceAsync();
            var pending = await _outboxRepository.GetPendingCountAsync();
            IsOnlineIntegration = true;
            StatusMessage = $"Integracao concluida. Enviadas: {sent}. Pendentes: {pending}.";
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha na integracao manual de vendas", ex);
            IsOnlineIntegration = false;
            StatusMessage = "Nao foi possivel integrar as vendas agora.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> AddBarcodeAsync()
    {
        if (_session.OpenCashRegister is null)
        {
            StatusMessage = "Abra o caixa para iniciar vendas.";
            return false;
        }

        BarcodeInput = BarcodeInput.Trim();
        if (string.IsNullOrWhiteSpace(BarcodeInput))
        {
            StatusMessage = "Informe um codigo de barras para adicionar.";
            return false;
        }

        var barcode = BarcodeInput;
        var temp = Items.ToList();
        var result = await _saleBuilderService.AddByBarcodeAsync(barcode, temp);
        if (!result.Added)
        {
            System.Media.SystemSounds.Beep.Play();
            StatusMessage = result.Error ?? "Falha ao adicionar item.";
            return false;
        }

        ReplaceItems(result.Items);
        var scannedItem = result.Items.FirstOrDefault(x => x.Barcode == barcode);
        if (scannedItem is not null)
        {
            UpdateLastScanned(scannedItem);
        }

        BarcodeInput = string.Empty;
        StatusMessage = "Item adicionado a venda.";
        return true;
    }

    public async Task<bool> AddProductByIdAsync(string productId)
    {
        if (_session.OpenCashRegister is null)
        {
            StatusMessage = "Abra o caixa para iniciar vendas.";
            return false;
        }

        var product = await _productCacheRepository.FindByIdAsync(productId);
        if (product is null || !product.Active)
        {
            StatusMessage = "Produto selecionado nao esta disponivel no catalogo local.";
            return false;
        }

        var existing = Items.FirstOrDefault(x => x.ProductId == product.ProductId);
        if (existing is not null)
        {
            existing.IncrementQuantity();
            UpdateLastScanned(existing);
        }
        else
        {
            var saleItem = new SaleItem
            {
                ProductId = product.ProductId,
                Barcode = product.Barcode,
                Description = product.Description,
                PriceCents = product.PriceCents
            };

            Items.Add(saleItem);
            UpdateLastScanned(saleItem);
        }

        NotifyTotals();
        StatusMessage = "Produto inserido manualmente na venda.";
        return true;
    }

    public async Task<bool> SelectCustomerByIdAsync(string customerId)
    {
        var customer = await _customerRepository.FindByIdAsync(customerId);
        if (customer is null)
        {
            StatusMessage = "Cliente selecionado nao foi encontrado.";
            return false;
        }

        SelectedCustomer = customer;
        StatusMessage = $"Cliente selecionado: {customer.Name}.";
        return true;
    }

    public void ClearSelectedCustomer()
    {
        SelectedCustomer = null;
        StatusMessage = "Cliente removido da venda.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            StatusMessage = "Selecione um item para remover.";
            return;
        }

        Items.Remove(SelectedItem);
        SelectedItem = null;
        NotifyTotals();
        StatusMessage = "Item removido.";
    }

    public void CancelSale()
    {
        Items.Clear();
        SelectedItem = null;
        SelectedCustomer = null;
        NotifyTotals();
        StatusMessage = "Venda cancelada.";
    }

    public bool UpdateItemQuantity(SaleItem? item, string? quantityText)
    {
        if (item is null)
        {
            StatusMessage = "Nenhum item selecionado para alterar quantidade.";
            return false;
        }

        if (!int.TryParse(quantityText, out var quantity) || quantity <= 0)
        {
            StatusMessage = "Quantidade invalida. Informe um numero inteiro maior que zero.";
            return false;
        }

        item.SetQuantity(quantity);
        NotifyTotals();
        if (LastScannedBarcode == item.Barcode)
        {
            UpdateLastScanned(item);
        }

        StatusMessage = "Quantidade atualizada.";
        return true;
    }

    public bool UpdateSelectedItemQuantity(string? quantityText)
    {
        return UpdateItemQuantity(SelectedItem, quantityText);
    }

    public async Task<Sale?> FinalizeAsync(PaymentMethod paymentMethod, int? receivedAmountCents = null)
    {
        if (IsBusy)
        {
            return null;
        }

        if (_session.OpenCashRegister is null)
        {
            StatusMessage = "Venda bloqueada: nenhum caixa aberto.";
            return null;
        }

        if (_session.OpenCashRegister.BusinessDate != DateTimeOffset.Now.ToString("yyyy-MM-dd"))
        {
            StatusMessage = "Caixa aberto em data anterior. Feche e reabra o caixa do dia.";
            return null;
        }

        if (!Items.Any())
        {
            StatusMessage = "Adicione itens para finalizar a venda.";
            return null;
        }

        if (paymentMethod == PaymentMethod.Cash)
        {
            if (!receivedAmountCents.HasValue || receivedAmountCents.Value < TotalCents)
            {
                StatusMessage = "Valor recebido em dinheiro deve ser maior ou igual ao total da venda.";
                return null;
            }
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Finalizando venda...";
            var changeAmountCents = paymentMethod == PaymentMethod.Cash && receivedAmountCents.HasValue
                ? receivedAmountCents.Value - TotalCents
                : 0;

            var sale = new Sale
            {
                SaleId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                PaymentMethod = paymentMethod,
                CustomerId = SelectedCustomer?.Id,
                CustomerName = SelectedCustomer?.Name,
                OperatorId = _session.CurrentUser?.Id,
                OperatorName = _session.CurrentUser?.FullName,
                ReceivedAmountCents = paymentMethod == PaymentMethod.Cash ? receivedAmountCents : null,
                ChangeAmountCents = changeAmountCents,
                CashRegisterSessionId = _session.OpenCashRegister.Id,
                Items = Items.Select(x => new SaleItem
                {
                    ProductId = x.ProductId,
                    Barcode = x.Barcode,
                    Description = x.Description,
                    PriceCents = x.PriceCents
                }).ToArray()
            };

            foreach (var item in sale.Items.Zip(Items))
            {
                item.First.SetQuantity(item.Second.Quantity);
            }

            var payload = JsonSerializer.Serialize(new
            {
                local_sale_id = sale.SaleId,
                session_id = _session.OpenCashRegister.Id,
                payment_method = sale.PaymentMethod.ToString().ToLowerInvariant(),
                customer_id = sale.CustomerId,
                items = sale.Items.Select(x => new
                {
                    product_id = x.ProductId,
                    barcode = x.Barcode,
                    quantity = x.Quantity
                })
            });

            await _salesRepository.SaveSaleWithOutboxAsync(sale, payload, _session.OpenCashRegister.Id);
            TriggerBackgroundSync();

            var pending = await _outboxRepository.GetPendingCountAsync();
            CancelSale();
            StatusMessage = $"Venda finalizada ({paymentMethod}) e enviada para integracao assincrona. Pendentes atuais: {pending}.";
            return sale;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao finalizar venda no PDV", ex);
            StatusMessage = "Nao foi possivel finalizar a venda. Tente novamente.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public (string StoreName, string StoreAddress, string StoreCnpj, string StoreLogoPath) GetStorePrintInfo()
    {
        return (StoreName, StoreAddress, StoreCnpj, StoreLogoPath);
    }

    private void TriggerBackgroundSync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _syncService.RunOnceAsync();
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha na integracao assincrona de venda", ex);
            }
        });
    }

    private void ReplaceItems(IReadOnlyCollection<SaleItem> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        NotifyTotals();
    }

    private void UpdateLastScanned(SaleItem item)
    {
        LastScannedDescription = item.Description;
        LastScannedBarcode = item.Barcode;
        LastScannedPriceCents = item.PriceCents;
        LastScannedQuantity = item.Quantity;
        OnPropertyChanged(nameof(LastScannedPriceFormatted));
        OnPropertyChanged(nameof(LastScannedQuantityText));
        OnPropertyChanged(nameof(SelectedItemUnitPriceFormatted));
        OnPropertyChanged(nameof(SelectedItemQuantityFormatted));
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalCents));
        OnPropertyChanged(nameof(TotalFormatted));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(ItemCountFormatted));
        OnPropertyChanged(nameof(SelectedItemQuantityFormatted));
        CancelSaleCommand.RaiseCanExecuteChanged();
    }

    private void NotifySelectionMetrics()
    {
        OnPropertyChanged(nameof(SelectedItemUnitPriceFormatted));
        OnPropertyChanged(nameof(SelectedItemQuantityFormatted));
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
