using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
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
    private readonly IProductsApiClient _productsApiClient;
    private readonly SyncService _syncService;
    private readonly SessionContext _session;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IPdvSettingsRepository _pdvSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private readonly AppRuntimeInfoService _runtimeInfo;

    private string _barcodeInput = string.Empty;
    private string _statusMessage = "PDV pronto para operacao.";
    private SaleItem? _selectedItem;
    private bool _isBusy;
    private string _lastScannedDescription = "Aguardando leitura de produto";
    private string _lastScannedDescriptionRaw = string.Empty;
    private string _lastScannedBarcode = "-";
    private int _lastScannedPriceCents;
    private int _lastScannedQuantity;
    private bool _isOnlineIntegration = true;
    private PdvSettings _settings = new();
    private Guid? _lastCompletedSaleId;
    private bool _canReprintLastSale;
    private string _currentFocusArea = "Leitura do codigo de barras";

    public MainViewModel(
        SaleBuilderService saleBuilderService,
        ISalesRepository salesRepository,
        IOutboxRepository outboxRepository,
        IProductCacheRepository productCacheRepository,
        ICustomerRepository customerRepository,
        ICatalogApiClient catalogApiClient,
        IProductsApiClient productsApiClient,
        SyncService syncService,
        PdvOptions options,
        SessionContext session,
        IStoreSettingsRepository storeSettingsRepository,
        IPdvSettingsRepository pdvSettingsRepository,
        IErrorFileLogger errorLogger,
        AppRuntimeInfoService runtimeInfo)
    {
        _saleBuilderService = saleBuilderService;
        _salesRepository = salesRepository;
        _outboxRepository = outboxRepository;
        _productCacheRepository = productCacheRepository;
        _customerRepository = customerRepository;
        _catalogApiClient = catalogApiClient;
        _productsApiClient = productsApiClient;
        _syncService = syncService;
        _session = session;
        _storeSettingsRepository = storeSettingsRepository;
        _pdvSettingsRepository = pdvSettingsRepository;
        _errorLogger = errorLogger;
        _runtimeInfo = runtimeInfo;

        DatabaseRelativePath = $"./{options.DatabaseRelativePath.Replace('\\', '/')}";
        DatabaseFullPath = options.DatabaseFullPath;

        RemoveSelectedCommand = new RelayCommand(RemoveSelectedItem, () => SelectedItem is not null);
    }

    public ObservableCollection<SaleItem> Items { get; } = [];
    public RelayCommand RemoveSelectedCommand { get; }

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
        private set => SetField(ref _isOnlineIntegration, value);
    }

    public string VersionLabel => _runtimeInfo.VersionLabel;
    public string IntegrationStatusText => IsOnlineIntegration ? "ONLINE" : "LOCAL";

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

    public string LastScannedPriceFormatted => MoneyFormatter.FormatFromCents(LastScannedPriceCents);
    public string LastScannedQuantityText => LastScannedQuantity <= 0 ? "Nenhum item lido" : $"Quantidade na venda: {LastScannedQuantity}";
    public string DatabaseRelativePath { get; }
    public string DatabaseFullPath { get; }
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
                OnPropertyChanged(nameof(CanEditSelectedItem));
            }
        }
    }

    public bool CanEditSelectedItem => SelectedItem is not null;

    public string SelectedItemUnitPriceFormatted => SelectedItem is null
        ? LastScannedPriceFormatted
        : MoneyFormatter.FormatFromCents(SelectedItem.PriceCents);

    public string SelectedItemQuantityFormatted => SelectedItem is null
        ? ItemCount.ToString("N0")
        : SelectedItem.Quantity.ToString("N0");

    public string ItemCountFormatted => ItemCount.ToString("N0");
    public bool CanReprintLastSale
    {
        get => _canReprintLastSale;
        private set => SetField(ref _canReprintLastSale, value);
    }
    public string CurrentFocusArea
    {
        get => _currentFocusArea;
        set => SetField(ref _currentFocusArea, value);
    }

    public string AddItemShortcutLabel => _settings.ShortcutAddItem;
    public string FinalizeShortcutLabel => _settings.ShortcutFinalizeSale;
    public string SearchProductShortcutLabel => _settings.ShortcutSearchProduct;
    public string ChangeQuantityShortcutLabel => _settings.ShortcutChangeQuantity;
    public string ChangePriceShortcutLabel => _settings.ShortcutChangePrice;
    public string SelectCustomerShortcutLabel => _settings.ShortcutSelectCustomer;
    public string ReprintLastSaleShortcutLabel => _settings.ShortcutReprintLastSale;
    public string RemoveItemShortcutLabel => _settings.ShortcutRemoveItem;
    public string CancelSaleShortcutLabel => _settings.ShortcutCancelSale;
    public decimal DefaultDiscountPercent => _settings.DefaultDiscountPercent;

    public async Task LoadAsync()
    {
        _settings = await _pdvSettingsRepository.GetCurrentAsync();
        OnPropertyChanged(nameof(AddItemShortcutLabel));
        OnPropertyChanged(nameof(FinalizeShortcutLabel));
        OnPropertyChanged(nameof(SearchProductShortcutLabel));
        OnPropertyChanged(nameof(ChangeQuantityShortcutLabel));
        OnPropertyChanged(nameof(ChangePriceShortcutLabel));
        OnPropertyChanged(nameof(SelectCustomerShortcutLabel));
        OnPropertyChanged(nameof(ReprintLastSaleShortcutLabel));
        OnPropertyChanged(nameof(RemoveItemShortcutLabel));
        OnPropertyChanged(nameof(CancelSaleShortcutLabel));
        OnPropertyChanged(nameof(DefaultDiscountPercent));
        RefreshDisplayedProductTexts();
        await LoadStoreSettingsAsync();
        await RefreshLastSaleAvailabilityAsync();
    }

    public async Task LoadStoreSettingsAsync()
    {
        var settings = await _storeSettingsRepository.GetCurrentAsync();
        IsOnlineIntegration = settings is not null;
    }

    public async Task RefreshSettingsAsync()
    {
        _settings = await _pdvSettingsRepository.GetCurrentAsync();
        OnPropertyChanged(nameof(AddItemShortcutLabel));
        OnPropertyChanged(nameof(FinalizeShortcutLabel));
        OnPropertyChanged(nameof(SearchProductShortcutLabel));
        OnPropertyChanged(nameof(ChangeQuantityShortcutLabel));
        OnPropertyChanged(nameof(ChangePriceShortcutLabel));
        OnPropertyChanged(nameof(SelectCustomerShortcutLabel));
        OnPropertyChanged(nameof(ReprintLastSaleShortcutLabel));
        OnPropertyChanged(nameof(RemoveItemShortcutLabel));
        OnPropertyChanged(nameof(CancelSaleShortcutLabel));
        OnPropertyChanged(nameof(DefaultDiscountPercent));
        RefreshDisplayedProductTexts();
    }

    public bool MatchesShortcut(Key key, string configuredShortcut)
    {
        var normalized = ShortcutKeyHelper.NormalizeKeyName(configuredShortcut);
        var current = ShortcutKeyHelper.NormalizeKeyName(key.ToString());
        return string.Equals(normalized, current, StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, ShortcutKeyHelper.NormalizeKeyName(ShortcutKeyHelper.ToDisplayString(key)), StringComparison.OrdinalIgnoreCase);
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
        RefreshDisplayedProductTexts();
        var scannedItem = Items.FirstOrDefault(x => x.Barcode == barcode);
        if (scannedItem is not null)
        {
            SelectedItem = scannedItem;
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
            ApplyProductTextCase(existing);
            SelectedItem = existing;
            UpdateLastScanned(existing);
        }
        else
        {
            var saleItem = new SaleItem
            {
                ProductId = product.ProductId,
                Barcode = product.Barcode,
                Description = product.Description,
                DisplayDescription = FormatProductText(product.Description),
                PriceCents = product.PriceCents
            };

            Items.Add(saleItem);
            SelectedItem = saleItem;
            UpdateLastScanned(saleItem);
        }

        NotifyTotals();
        StatusMessage = "Produto inserido manualmente na venda.";
        return true;
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
        OnPropertyChanged(nameof(CanEditSelectedItem));
        StatusMessage = "Item removido.";
    }

    public void CancelSale()
    {
        Items.Clear();
        SelectedItem = null;
        LastScannedDescription = "Aguardando leitura de produto";
        _lastScannedDescriptionRaw = string.Empty;
        LastScannedBarcode = "-";
        LastScannedPriceCents = 0;
        LastScannedQuantity = 0;
        OnPropertyChanged(nameof(LastScannedPriceFormatted));
        OnPropertyChanged(nameof(LastScannedQuantityText));
        OnPropertyChanged(nameof(SelectedItemUnitPriceFormatted));
        OnPropertyChanged(nameof(SelectedItemQuantityFormatted));
        NotifyTotals();
        OnPropertyChanged(nameof(CanEditSelectedItem));
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

    public async Task<bool> UpdateItemPriceAsync(SaleItem? item, string? priceText, bool updateBaseProductPrice)
    {
        if (item is null)
        {
            StatusMessage = "Nenhum item selecionado para alterar preco.";
            return false;
        }

        if (!MoneyFormatter.TryParseToCents(priceText, out var priceCents) || priceCents < 0)
        {
            StatusMessage = "Preco invalido.";
            return false;
        }

        item.SetPrice(priceCents);
        NotifyTotals();
        if (LastScannedBarcode == item.Barcode)
        {
            UpdateLastScanned(item);
        }

        if (!updateBaseProductPrice)
        {
            StatusMessage = "Preco atualizado apenas neste item.";
            return true;
        }

        var cachedProduct = await _productCacheRepository.FindByIdAsync(item.ProductId);
        if (cachedProduct is not null)
        {
            cachedProduct.PriceCents = priceCents;
            cachedProduct.UpdatedAt = DateTimeOffset.UtcNow;
            await _productCacheRepository.UpdateAsync(cachedProduct);
        }

        try
        {
            await _productsApiClient.UpdatePriceAsync(item.ProductId, priceCents);
            IsOnlineIntegration = true;
            StatusMessage = "Preco atualizado na venda e no cadastro do produto.";
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao atualizar preco base do produto no PDV", ex);
            IsOnlineIntegration = false;
            StatusMessage = "Preco atualizado na venda e salvo localmente. Nao foi possivel atualizar a API agora.";
        }

        return true;
    }

    public async Task<CustomerRecord?> GetCustomerByIdAsync(string customerId)
    {
        return await _customerRepository.FindByIdAsync(customerId);
    }

    public async Task<Sale?> FinalizeAsync(SaleCheckoutRequest request)
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

        if (request.PaymentMethod == PaymentMethod.Cash)
        {
            if (!request.ReceivedAmountCents.HasValue || request.ReceivedAmountCents.Value < 0)
            {
                StatusMessage = "Informe um valor valido em dinheiro.";
                return null;
            }
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Finalizando venda...";
            var subtotalCents = Items.Sum(x => x.SubtotalCents);
            var discountPercent = Math.Clamp(request.DiscountPercent, 0m, 100m);
            var discountCents = (int)Math.Round(subtotalCents * (discountPercent / 100m), MidpointRounding.AwayFromZero);
            var effectiveSubtotalCents = Math.Max(subtotalCents - discountCents, 0);
            var receivedAmountCents = request.PaymentMethod == PaymentMethod.Cash
                ? request.ReceivedAmountCents ?? 0
                : 0;
            var cashShortageAsDiscountCents = 0;
            var finalTotalCents = effectiveSubtotalCents;

            if (request.PaymentMethod == PaymentMethod.Cash && request.ReceivedAmountCents.HasValue && request.ReceivedAmountCents.Value < effectiveSubtotalCents)
            {
                cashShortageAsDiscountCents = effectiveSubtotalCents - request.ReceivedAmountCents.Value;
                finalTotalCents = request.ReceivedAmountCents.Value;
            }

            var totalDiscountCents = discountCents + cashShortageAsDiscountCents;
            var changeAmountCents = request.PaymentMethod == PaymentMethod.Cash
                ? Math.Max(receivedAmountCents - finalTotalCents, 0)
                : 0;

            var sale = new Sale
            {
                SaleId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                PaymentMethod = request.PaymentMethod,
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                OperatorId = _session.CurrentUser?.Id,
                OperatorName = _session.CurrentUser?.FullName,
                ReceivedAmountCents = request.PaymentMethod == PaymentMethod.Cash ? request.ReceivedAmountCents : null,
                ChangeAmountCents = changeAmountCents,
                CashRegisterSessionId = _session.OpenCashRegister.Id,
                DiscountPercent = discountPercent,
                DiscountCents = totalDiscountCents,
                ReceiptRequested = request.ReceiptRequested,
                ReceiptTaxId = request.ReceiptTaxId,
                Items = Items.Select(x => new SaleItem
                {
                    ProductId = x.ProductId,
                    Barcode = x.Barcode,
                    Description = x.Description,
                    DisplayDescription = FormatProductText(x.Description),
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
                payment_method = GetPaymentCode(sale.PaymentMethod),
                customer_id = sale.CustomerId,
                discount_percent = sale.DiscountPercent,
                receipt_requested = sale.ReceiptRequested,
                receipt_tax_id = string.IsNullOrWhiteSpace(sale.ReceiptTaxId) ? null : sale.ReceiptTaxId,
                items = sale.Items.Select(x => new
                {
                    product_id = x.ProductId,
                    barcode = x.Barcode,
                    quantity = x.Quantity,
                    unit_price = x.PriceCents / 100m
                })
            });

            await _salesRepository.SaveSaleWithOutboxAsync(sale, payload, _session.OpenCashRegister.Id);
            TriggerBackgroundSync();
            _lastCompletedSaleId = sale.SaleId;
            CanReprintLastSale = true;

            var pending = await _outboxRepository.GetPendingCountAsync();
            CancelSale();
            StatusMessage = $"Venda finalizada ({GetPaymentLabel(sale.PaymentMethod)}) e enviada para integracao assincrona. Pendentes atuais: {pending}.";
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

    public async Task<(StoreSettings? StoreSettings, PdvSettings Settings)> GetPrintContextAsync()
    {
        var storeSettings = await _storeSettingsRepository.GetCurrentAsync();
        var settings = await _pdvSettingsRepository.GetCurrentAsync();
        return (storeSettings, settings);
    }

    public async Task<Sale?> GetLastSaleForPrintAsync()
    {
        if (_lastCompletedSaleId.HasValue)
        {
            var currentSessionSale = await _salesRepository.FindByIdAsync(_lastCompletedSaleId.Value);
            if (currentSessionSale is not null)
            {
                return currentSessionSale;
            }
        }

        return await _salesRepository.GetLatestCompletedSaleAsync(_session.OpenCashRegister?.Id);
    }

    public async Task RefreshLastSaleAvailabilityAsync()
    {
        if (_session.OpenCashRegister is null)
        {
            CanReprintLastSale = false;
            return;
        }

        var sale = await _salesRepository.GetLatestCompletedSaleAsync(_session.OpenCashRegister.Id);
        _lastCompletedSaleId = sale?.SaleId;
        CanReprintLastSale = sale is not null;
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
        _lastScannedDescriptionRaw = item.Description;
        ApplyProductTextCase(item);
        LastScannedDescription = string.IsNullOrWhiteSpace(item.DisplayDescription)
            ? FormatProductText(item.Description)
            : item.DisplayDescription;
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
    }

    private void NotifySelectionMetrics()
    {
        OnPropertyChanged(nameof(SelectedItemUnitPriceFormatted));
        OnPropertyChanged(nameof(SelectedItemQuantityFormatted));
    }

    private string FormatProductText(string? value)
    {
        return ProductTextFormatter.Format(value, _settings.ProductTextCase);
    }

    private void ApplyProductTextCase(SaleItem item)
    {
        item.DisplayDescription = FormatProductText(item.Description);
    }

    private void RefreshDisplayedProductTexts()
    {
        foreach (var item in Items)
        {
            ApplyProductTextCase(item);
        }

        if (SelectedItem is not null)
        {
            ApplyProductTextCase(SelectedItem);
        }

        if (!string.IsNullOrWhiteSpace(_lastScannedDescriptionRaw))
        {
            LastScannedDescription = FormatProductText(_lastScannedDescriptionRaw);
        }

        OnPropertyChanged(nameof(LastScannedPriceFormatted));
        OnPropertyChanged(nameof(LastScannedQuantityText));
        OnPropertyChanged(nameof(LastScannedDescription));
    }

    private static string GetPaymentCode(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash => "cash",
            PaymentMethod.CreditCard => "credit_card",
            PaymentMethod.DebitCard => "debit_card",
            PaymentMethod.Pix => "pix",
            _ => paymentMethod.ToString().ToLowerInvariant()
        };
    }

    private static string GetPaymentLabel(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash => "Dinheiro",
            PaymentMethod.CreditCard => "Cartao de credito",
            PaymentMethod.DebitCard => "Cartao de debito",
            PaymentMethod.Pix => "PIX",
            _ => paymentMethod.ToString()
        };
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
