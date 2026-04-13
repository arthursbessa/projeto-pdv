using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Services;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class SalesHistoryViewModel : INotifyPropertyChanged
{
    private readonly ISalesRepository _salesRepository;
    private readonly IRefundsApiClient _refundsApiClient;
    private readonly IStoreSettingsRepository _storeSettingsRepository;
    private readonly IPdvSettingsRepository _pdvSettingsRepository;
    private readonly SyncService _syncService;
    private readonly SessionContext _session;
    private readonly IErrorFileLogger _errorLogger;
    private readonly List<SaleHistoryItemViewModel> _allSales = [];
    private DateTime _selectedDate = DateTime.Today;
    private string _statusMessage = "Selecione uma data para carregar as vendas.";
    private bool _isBusy;
    private SaleHistoryItemViewModel? _selectedSale;
    private Sale? _selectedSaleDetails;
    private string _refundReason = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedCustomerFilter = "Todos";
    private string _selectedCashierFilter = "Todos";

    public SalesHistoryViewModel(
        ISalesRepository salesRepository,
        IRefundsApiClient refundsApiClient,
        IStoreSettingsRepository storeSettingsRepository,
        IPdvSettingsRepository pdvSettingsRepository,
        SyncService syncService,
        SessionContext session,
        IErrorFileLogger errorLogger)
    {
        _salesRepository = salesRepository;
        _refundsApiClient = refundsApiClient;
        _storeSettingsRepository = storeSettingsRepository;
        _pdvSettingsRepository = pdvSettingsRepository;
        _syncService = syncService;
        _session = session;
        _errorLogger = errorLogger;
    }

    public ObservableCollection<SaleHistoryItemViewModel> Sales { get; } = [];
    public ObservableCollection<SaleDetailItemViewModel> SaleItems { get; } = [];
    public ObservableCollection<string> CustomerFilters { get; } = [];
    public ObservableCollection<string> CashierFilters { get; } = [];

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetField(ref _selectedDate, value.Date);
    }

    public SaleHistoryItemViewModel? SelectedSale
    {
        get => _selectedSale;
        set
        {
            if (SetField(ref _selectedSale, value))
            {
                _ = LoadSelectedSaleAsync();
            }
        }
    }

    public string RefundReason
    {
        get => _refundReason;
        set => SetField(ref _refundReason, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCustomerFilter
    {
        get => _selectedCustomerFilter;
        set
        {
            if (SetField(ref _selectedCustomerFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCashierFilter
    {
        get => _selectedCashierFilter;
        set
        {
            if (SetField(ref _selectedCashierFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public DateTime MinDate => DateTime.Today.AddDays(-6);
    public DateTime MaxDate => DateTime.Today;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public bool HasSelectedSale => _selectedSaleDetails is not null;
    public bool CanOpenSelectedSale => SelectedSale is not null;
    public bool CanRefundSelectedSale => _selectedSaleDetails is not null
        && SaleItems.Any(x => x.RemainingQuantity > 0);

    public Sale? SelectedSaleDetails => _selectedSaleDetails;
    public string SelectedSaleIdentifier => _selectedSaleDetails?.RemoteSaleId ?? _selectedSaleDetails?.SaleId.ToString() ?? "-";
    public string SelectedSaleCustomer => _selectedSaleDetails?.CustomerName ?? "Consumidor final";
    public string SelectedSaleCashier => _selectedSaleDetails?.OperatorName ?? "-";
    public string SelectedSaleStatus => FormatStatus(_selectedSaleDetails?.Status);
    public string SelectedSaleDate => _selectedSaleDetails?.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") ?? "-";
    public string SelectedSalePayment => _selectedSaleDetails is null ? "-" : FormatPayment(_selectedSaleDetails.PaymentMethod);
    public string SelectedSaleTotal => _selectedSaleDetails is null ? "-" : MoneyFormatter.FormatFromCents(_selectedSaleDetails.TotalCents);
    public string SelectedSaleReceived => _selectedSaleDetails?.ReceivedAmountCents is int received ? MoneyFormatter.FormatFromCents(received) : "-";
    public string SelectedSaleChange => _selectedSaleDetails is null ? "-" : MoneyFormatter.FormatFromCents(_selectedSaleDetails.ChangeAmountCents);
    public string SelectedSaleDiscount => _selectedSaleDetails is null ? "-" : MoneyFormatter.FormatFromCents(_selectedSaleDetails.DiscountCents);
    public string SelectedSaleReceiptTaxId => string.IsNullOrWhiteSpace(_selectedSaleDetails?.ReceiptTaxId) ? "-" : _selectedSaleDetails!.ReceiptTaxId!;
    public string SelectedSalePrintedStatus => _selectedSaleDetails?.PrintedAt is null
        ? "Nao impresso"
        : $"Impresso em {_selectedSaleDetails.PrintedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm:ss}";

    public async Task LoadAsync()
    {
        if (SelectedDate < MinDate || SelectedDate > MaxDate)
        {
            StatusMessage = "Selecione uma data dentro dos ultimos 7 dias, incluindo hoje.";
            return;
        }

        IsBusy = true;
        try
        {
            var sales = await _salesRepository.GetHistoryAsync(SelectedDate);
            _allSales.Clear();

            foreach (var sale in sales)
            {
                _allSales.Add(new SaleHistoryItemViewModel
                {
                    SaleId = sale.SaleId,
                    SaleIdentifier = sale.SaleIdentifier,
                    CreatedAt = sale.CreatedAt,
                    PaymentMethod = sale.PaymentMethod,
                    CustomerName = sale.CustomerName,
                    CashierName = sale.CashierName,
                    ProductsSummary = sale.ProductsSummary,
                    Status = sale.Status,
                    TotalCents = sale.TotalCents,
                    ReceivedAmountCents = sale.ReceivedAmountCents,
                    ChangeAmountCents = sale.ChangeAmountCents
                });
            }

            RefreshFilterCollections();
            ApplyFilters();

            StatusMessage = Sales.Count == 0
                ? "Nenhuma venda encontrada para a data selecionada."
                : $"{Sales.Count} venda(s) encontrada(s).";

            if (SelectedSale is null && Sales.Count > 0)
            {
                SelectedSale = Sales[0];
            }
            else if (SelectedSale is not null)
            {
                var reselected = Sales.FirstOrDefault(x => x.SaleId == SelectedSale.SaleId);
                SelectedSale = reselected;
            }
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao carregar tela de vendas", ex);
            StatusMessage = "Nao foi possivel carregar as vendas agora.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RegisterRefundAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (_selectedSaleDetails is null)
        {
            StatusMessage = "Selecione uma venda para registrar a devolucao.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RefundReason))
        {
            StatusMessage = "Informe o motivo da devolucao.";
            return false;
        }

        var refundItems = new List<SaleRefundItem>();
        foreach (var item in SaleItems)
        {
            if (!item.TryGetRefundQuantity(out var quantity))
            {
                continue;
            }

            if (quantity <= 0)
            {
                StatusMessage = "A quantidade de devolucao deve ser maior que zero.";
                return false;
            }

            if (quantity > item.RemainingQuantity)
            {
                StatusMessage = $"A devolucao do item '{item.Description}' excede a quantidade disponivel.";
                return false;
            }

            refundItems.Add(new SaleRefundItem
            {
                SaleItemId = item.SaleItemId,
                ProductId = item.ProductId,
                Description = item.Description,
                Quantity = quantity
            });
        }

        if (refundItems.Count == 0)
        {
            StatusMessage = "Informe ao menos um item e quantidade para devolucao.";
            return false;
        }

        IsBusy = true;
        try
        {
            var currentSaleId = _selectedSaleDetails.SaleId;
            var payloadItems = refundItems
                .GroupBy(x => x.ProductId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    product_id = group.Key,
                    quantity = group.Sum(x => x.Quantity)
                })
                .ToArray();

            var payload = JsonSerializer.Serialize(new
            {
                sale_id = _selectedSaleDetails.RemoteSaleId ?? _selectedSaleDetails.SaleId.ToString(),
                reason = RefundReason.Trim(),
                items = payloadItems
            });

            await _refundsApiClient.RegisterRefundAsync(payload);

            try
            {
                await _salesRepository.SaveRefundAsync(
                    _selectedSaleDetails.SaleId,
                    RefundReason.Trim(),
                    refundItems,
                    _session.CurrentUser?.Id);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao atualizar devolucao local apos resposta da API", ex);
                StatusMessage = "Devolucao enviada. Reabra a venda para atualizar os detalhes.";
                return true;
            }

            RefundReason = string.Empty;
            await LoadAsync();
            SelectedSale = Sales.FirstOrDefault(x => x.SaleId == currentSaleId);
            StatusMessage = "Devolucao registrada com sucesso.";
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao registrar devolucao no PDV", ex);
            StatusMessage = "Nao foi possivel registrar a devolucao.";
            return false;
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

    public async Task MarkSelectedSalePrintedAsync(string? receiptTaxId)
    {
        if (_selectedSaleDetails is null)
        {
            return;
        }

        var printedAt = DateTimeOffset.UtcNow;
        var effectiveTaxId = string.IsNullOrWhiteSpace(receiptTaxId) ? _selectedSaleDetails.ReceiptTaxId : receiptTaxId;
        var payload = JsonSerializer.Serialize(new
        {
            local_sale_id = _selectedSaleDetails.SaleId,
            sale_id = _selectedSaleDetails.RemoteSaleId,
            printed_at = printedAt.ToString("O"),
            receipt_tax_id = effectiveTaxId
        });

        await _salesRepository.MarkAsPrintedAsync(_selectedSaleDetails.SaleId, printedAt, effectiveTaxId, payload);
        _selectedSaleDetails = await _salesRepository.FindByIdAsync(_selectedSaleDetails.SaleId);
        NotifySelectedSaleChanged();
        TriggerBackgroundSync();
    }

    private async Task LoadSelectedSaleAsync()
    {
        if (SelectedSale is null)
        {
            _selectedSaleDetails = null;
            SaleItems.Clear();
            RefundReason = string.Empty;
            NotifySelectedSaleChanged();
            return;
        }

        try
        {
            _selectedSaleDetails = await _salesRepository.FindByIdAsync(SelectedSale.SaleId);
            SaleItems.Clear();

            foreach (var item in _selectedSaleDetails?.Items ?? [])
            {
                SaleItems.Add(new SaleDetailItemViewModel
                {
                    SaleItemId = item.SaleItemId ?? string.Empty,
                    ProductId = item.ProductId,
                    Barcode = item.Barcode,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    RefundedQuantity = item.RefundedQuantity,
                    RemainingQuantity = item.RemainingRefundQuantity,
                    PriceCents = item.PriceCents,
                    SubtotalCents = item.SubtotalCents
                });
            }

            RefundReason = string.Empty;
            NotifySelectedSaleChanged();
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao carregar detalhes da venda", ex);
            StatusMessage = "Nao foi possivel carregar os detalhes da venda.";
        }
    }

    private void NotifySelectedSaleChanged()
    {
        OnPropertyChanged(nameof(CanOpenSelectedSale));
        OnPropertyChanged(nameof(HasSelectedSale));
        OnPropertyChanged(nameof(CanRefundSelectedSale));
        OnPropertyChanged(nameof(SelectedSaleDetails));
        OnPropertyChanged(nameof(SelectedSaleIdentifier));
        OnPropertyChanged(nameof(SelectedSaleCustomer));
        OnPropertyChanged(nameof(SelectedSaleCashier));
        OnPropertyChanged(nameof(SelectedSaleStatus));
        OnPropertyChanged(nameof(SelectedSaleDate));
        OnPropertyChanged(nameof(SelectedSalePayment));
        OnPropertyChanged(nameof(SelectedSaleTotal));
        OnPropertyChanged(nameof(SelectedSaleReceived));
        OnPropertyChanged(nameof(SelectedSaleChange));
        OnPropertyChanged(nameof(SelectedSaleDiscount));
        OnPropertyChanged(nameof(SelectedSaleReceiptTaxId));
        OnPropertyChanged(nameof(SelectedSalePrintedStatus));
    }

    private void RefreshFilterCollections()
    {
        var currentCustomer = SelectedCustomerFilter;
        var currentCashier = SelectedCashierFilter;

        CustomerFilters.Clear();
        CustomerFilters.Add("Todos");
        foreach (var customer in _allSales
                     .Select(x => x.CustomerName)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            CustomerFilters.Add(customer);
        }

        CashierFilters.Clear();
        CashierFilters.Add("Todos");
        foreach (var cashier in _allSales
                     .Select(x => x.CashierName)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            CashierFilters.Add(cashier);
        }

        SelectedCustomerFilter = CustomerFilters.Contains(currentCustomer) ? currentCustomer : "Todos";
        SelectedCashierFilter = CashierFilters.Contains(currentCashier) ? currentCashier : "Todos";
    }

    private void ApplyFilters()
    {
        var filteredSales = _allSales.Where(MatchesFilters).ToList();
        Sales.Clear();
        foreach (var sale in filteredSales)
        {
            Sales.Add(sale);
        }

        if (SelectedSale is not null && Sales.All(x => x.SaleId != SelectedSale.SaleId))
        {
            SelectedSale = null;
        }

        if (SelectedSale is null && Sales.Count > 0)
        {
            SelectedSale = Sales[0];
        }
    }

    private bool MatchesFilters(SaleHistoryItemViewModel sale)
    {
        if (!string.Equals(SelectedCustomerFilter, "Todos", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sale.CustomerName, SelectedCustomerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(SelectedCashierFilter, "Todos", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sale.CashierName, SelectedCashierFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var query = SearchText.Trim();
        return sale.SaleIdentifier.Contains(query, StringComparison.OrdinalIgnoreCase)
               || sale.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || sale.CashierName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || sale.ProductsSummary.Contains(query, StringComparison.OrdinalIgnoreCase)
               || sale.DateText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatStatus(string? status)
    {
        return status switch
        {
            "REFUNDED" => "Devolvida",
            "PARTIALLY_REFUNDED" => "Devolucao parcial",
            _ => "Concluida"
        };
    }

    private static string FormatPayment(PaymentMethod paymentMethod)
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
                _errorLogger.LogError("Falha na integracao assincrona das vendas", ex);
            }
        });
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
