using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Services;

namespace Pdv.Ui.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SaleBuilderService _saleBuilderService;
    private readonly ISalesRepository _salesRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly IProductCacheRepository _productCacheRepository;
    private readonly SyncService _syncService;
    private readonly DispatcherTimer _syncTimer;

    private string _barcodeInput = string.Empty;
    private string _connectionStatus = "Desconhecida";
    private int _pendingCount;
    private string? _receivedAmountInput;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    private SaleItem? _selectedItem;

    public MainViewModel(
        SaleBuilderService saleBuilderService,
        ISalesRepository salesRepository,
        IOutboxRepository outboxRepository,
        ICatalogApiClient catalogApiClient,
        IProductCacheRepository productCacheRepository,
        SyncService syncService,
        Pdv.Application.Configuration.PdvOptions options)
    {
        _saleBuilderService = saleBuilderService;
        _salesRepository = salesRepository;
        _outboxRepository = outboxRepository;
        _catalogApiClient = catalogApiClient;
        _productCacheRepository = productCacheRepository;
        _syncService = syncService;

        SyncNowCommand = new RelayCommand(SyncNowAsync);
        RefreshCatalogCommand = new RelayCommand(RefreshCatalogAsync);
        FinalizeCommand = new RelayCommand(FinalizeAsync, () => Items.Any());

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(5, options.SyncIntervalSeconds))
        };
        _syncTimer.Tick += async (_, _) => await SyncNowAsync();
        _syncTimer.Start();

        _ = RefreshPendingCountAsync();
    }

    public ObservableCollection<SaleItem> Items { get; } = [];
    public RelayCommand SyncNowCommand { get; }
    public RelayCommand RefreshCatalogCommand { get; }
    public RelayCommand FinalizeCommand { get; }

    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetField(ref _barcodeInput, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        private set => SetField(ref _pendingCount, value);
    }

    public decimal Total => Items.Sum(x => x.Subtotal);

    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
    [
        PaymentMethod.Cash,
        PaymentMethod.Card,
        PaymentMethod.Pix
    ];

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetField(ref _selectedPaymentMethod, value);
    }

    public string? ReceivedAmountInput
    {
        get => _receivedAmountInput;
        set => SetField(ref _receivedAmountInput, value);
    }

    public SaleItem? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public async Task AddBarcodeAsync()
    {
        var temp = Items.ToList();
        var result = await _saleBuilderService.AddByBarcodeAsync(BarcodeInput, temp);

        if (!result.Added)
        {
            MessageBox.Show(result.Error ?? "Falha ao adicionar item.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            BarcodeInput = string.Empty;
            return;
        }

        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }

        BarcodeInput = string.Empty;
        OnPropertyChanged(nameof(Total));
        FinalizeCommand.RaiseCanExecuteChanged();
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        Items.Remove(SelectedItem);
        OnPropertyChanged(nameof(Total));
        FinalizeCommand.RaiseCanExecuteChanged();
    }

    public void CancelSale()
    {
        Items.Clear();
        ReceivedAmountInput = null;
        OnPropertyChanged(nameof(Total));
        FinalizeCommand.RaiseCanExecuteChanged();
    }

    public async Task FinalizeAsync()
    {
        if (!Items.Any())
        {
            return;
        }

        decimal? receivedAmount = null;
        if (decimal.TryParse(ReceivedAmountInput, out var parsed))
        {
            receivedAmount = parsed;
        }

        var sale = new Sale
        {
            SaleId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            PaymentMethod = SelectedPaymentMethod,
            ReceivedAmount = receivedAmount,
            Items = Items.Select(x => new SaleItem
            {
                ProductId = x.ProductId,
                Barcode = x.Barcode,
                Description = x.Description,
                UnitPrice = x.UnitPrice
            }).ToArray()
        };

        foreach (var item in sale.Items.Zip(Items))
        {
            for (var i = 1; i < item.Second.Quantity; i++)
            {
                item.First.IncrementQuantity();
            }
        }

        var payload = JsonSerializer.Serialize(new
        {
            sale_id = sale.SaleId,
            created_at = sale.CreatedAt,
            payment_method = sale.PaymentMethod.ToString(),
            received_amount = sale.ReceivedAmount,
            total = sale.Total,
            items = sale.Items.Select(x => new
            {
                product_id = x.ProductId,
                barcode = x.Barcode,
                description = x.Description,
                quantity = x.Quantity,
                unit_price = x.UnitPrice,
                subtotal = x.Subtotal
            })
        });

        await _salesRepository.SaveSaleWithOutboxAsync(sale, payload);
        CancelSale();
        await RefreshPendingCountAsync();
    }

    public async Task SyncNowAsync()
    {
        try
        {
            var sent = await _syncService.RunOnceAsync();
            ConnectionStatus = "Online";
            await RefreshPendingCountAsync();
            if (sent > 0)
            {
                // intentionally silent
            }
        }
        catch
        {
            ConnectionStatus = "Offline";
        }
    }

    public async Task RefreshCatalogAsync()
    {
        try
        {
            var catalog = await _catalogApiClient.GetCatalogAsync();
            await _productCacheRepository.ReplaceCatalogAsync(catalog);
            ConnectionStatus = "Online";
            MessageBox.Show("Catálogo atualizado com sucesso.", "PDV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (HttpRequestException)
        {
            ConnectionStatus = "Offline";
            MessageBox.Show("Sem internet. Mantendo catálogo local.", "PDV", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RefreshPendingCountAsync()
    {
        PendingCount = await _outboxRepository.GetPendingCountAsync();
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
