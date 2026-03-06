using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Application.Services;
using Pdv.Ui.Formatting;

namespace Pdv.Ui.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SaleBuilderService _saleBuilderService;
    private readonly ISalesRepository _salesRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IProductCacheRepository _productCacheRepository;
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly SyncService _syncService;
    private readonly SessionContext _session;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private string _barcodeInput = string.Empty;
    private string _statusMessage = "PDV iniciado.";
    private SaleItem? _selectedItem;
    private bool _isBusy;
    private string _storeName = "LOJA";
    private string _storeCnpj = string.Empty;
    private string _storeAddress = string.Empty;
    private string _storeLogoPath = string.Empty;

    public MainViewModel(
        SaleBuilderService saleBuilderService,
        ISalesRepository salesRepository,
        IOutboxRepository outboxRepository,
        IProductCacheRepository productCacheRepository,
        ICatalogApiClient catalogApiClient,
        SyncService syncService,
        PdvOptions options,
        SessionContext session,
        IStoreSettingsRepository storeSettingsRepository)
    {
        _saleBuilderService = saleBuilderService;
        _salesRepository = salesRepository;
        _outboxRepository = outboxRepository;
        _productCacheRepository = productCacheRepository;
        _catalogApiClient = catalogApiClient;
        _syncService = syncService;
        _session = session;
        _storeSettingsRepository = storeSettingsRepository;

        DatabaseRelativePath = $"./{options.DatabaseRelativePath.Replace('\\', '/')}";
        DatabaseFullPath = options.DatabaseFullPath;

        RemoveSelectedCommand = new RelayCommand(RemoveSelectedItem, () => SelectedItem is not null);
        CancelSaleCommand = new RelayCommand(CancelSale, () => Items.Any());
    }

    public ObservableCollection<SaleItem> Items { get; } = [];
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand CancelSaleCommand { get; }

    public string BarcodeInput { get => _barcodeInput; set => SetField(ref _barcodeInput, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
    public string OfflineStatus => "Integração automática ativa";
    public string StoreName { get => _storeName; set => SetField(ref _storeName, value); }
    public string StoreCnpj { get => _storeCnpj; set => SetField(ref _storeCnpj, value); }
    public string StoreAddress { get => _storeAddress; set => SetField(ref _storeAddress, value); }
    public string StoreLogoPath { get => _storeLogoPath; set => SetField(ref _storeLogoPath, value); }
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
            }
        }
    }


    public async Task LoadStoreSettingsAsync()
    {
        var settings = await _storeSettingsRepository.GetCurrentAsync();
        if (settings is null)
        {
            return;
        }

        StoreName = settings.StoreName;
        StoreCnpj = settings.Cnpj;
        StoreAddress = settings.Address;
        StoreLogoPath = settings.LogoLocalPath;
    }

    public async Task RefreshCatalogAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            StatusMessage = "Sincronizando catálogo...";
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

            StatusMessage = $"Catálogo sincronizado: {remoteProducts.Count} item(ns).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao atualizar catálogo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SyncSalesAsync()
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
            StatusMessage = $"Falha na integração: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddBarcodeAsync()
    {
        if (_session.OpenCashRegister is null)
        {
            StatusMessage = "Abra o caixa para iniciar vendas.";
            return;
        }

        BarcodeInput = BarcodeInput.Trim();

        if (string.IsNullOrWhiteSpace(BarcodeInput))
        {
            StatusMessage = "Informe um código de barras para adicionar.";
            return;
        }

        var temp = Items.ToList();
        var result = await _saleBuilderService.AddByBarcodeAsync(BarcodeInput, temp);

        if (!result.Added)
        {
            System.Media.SystemSounds.Beep.Play();
            StatusMessage = result.Error ?? "Falha ao adicionar item.";
            BarcodeInput = string.Empty;
            return;
        }

        ReplaceItems(result.Items);
        BarcodeInput = string.Empty;
        StatusMessage = "Item adicionado à venda.";
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
            StatusMessage = "Quantidade inválida. Informe um número inteiro maior que zero.";
            return false;
        }

        item.SetQuantity(quantity);
        NotifyTotals();
        StatusMessage = "Quantidade atualizada.";
        return true;
    }

    public bool UpdateSelectedItemQuantity(string? quantityText)
    {
        return UpdateItemQuantity(SelectedItem, quantityText);
    }

    public async Task<Sale?> FinalizeAsync(PaymentMethod paymentMethod)
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

        IsBusy = true;
        try
        {
            StatusMessage = "Finalizando venda...";
            var sale = new Sale
            {
                SaleId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                PaymentMethod = paymentMethod,
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
                session_id = _session.OpenCashRegister.Id,
                payment_method = sale.PaymentMethod.ToString().ToLowerInvariant(),
                items = sale.Items.Select(x => new { product_id = x.ProductId, quantity = x.Quantity })
            });

            await _salesRepository.SaveSaleWithOutboxAsync(sale, payload, _session.OpenCashRegister.Id);

            var sent = await _syncService.RunOnceAsync();
            var pending = await _outboxRepository.GetPendingCountAsync();

            CancelSale();
            StatusMessage = sent > 0
                ? $"Venda finalizada ({paymentMethod}) e integrada automaticamente. Pendentes: {pending}."
                : $"Venda finalizada ({paymentMethod}) e salva offline. Outbox pendente: {pending}.";
            return sale;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceItems(IReadOnlyCollection<SaleItem> items)
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        NotifyTotals();
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalCents));
        OnPropertyChanged(nameof(TotalFormatted));
        OnPropertyChanged(nameof(ItemCount));
        CancelSaleCommand.RaiseCanExecuteChanged();
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
