using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
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
    private readonly string _dbPath;
    private string _barcodeInput = string.Empty;
    private string _statusMessage = "PDV local iniciado.";
    private SaleItem? _selectedItem;

    public MainViewModel(SaleBuilderService saleBuilderService, ISalesRepository salesRepository, IOutboxRepository outboxRepository, PdvOptions options)
    {
        _saleBuilderService = saleBuilderService;
        _salesRepository = salesRepository;
        _outboxRepository = outboxRepository;
        _dbPath = options.DatabaseRelativePath;

        FinalizeCommand = new RelayCommand(FinalizeAsync, () => Items.Any());
        RemoveSelectedCommand = new RelayCommand(RemoveSelectedItem, () => SelectedItem is not null);
        CancelSaleCommand = new RelayCommand(CancelSale);
    }

    public ObservableCollection<SaleItem> Items { get; } = [];
    public RelayCommand FinalizeCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand CancelSaleCommand { get; }

    public string BarcodeInput { get => _barcodeInput; set => SetField(ref _barcodeInput, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string OfflineStatus => "Offline Local";
    public string DatabaseStatus => $"SQLite: {_dbPath}";
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

    public async Task AddBarcodeAsync()
    {
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
        StatusMessage = "Item adicionado.";
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null) return;
        Items.Remove(SelectedItem);
        SelectedItem = null;
        NotifyTotals();
        StatusMessage = "Item removido.";
    }

    public void CancelSale()
    {
        Items.Clear();
        NotifyTotals();
        StatusMessage = "Venda cancelada.";
    }

    public async Task FinalizeAsync()
    {
        if (!Items.Any()) return;

        var sale = new Sale
        {
            SaleId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            PaymentMethod = PaymentMethod.Cash,
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
            for (var i = 1; i < item.Second.Quantity; i++) item.First.IncrementQuantity();
        }

        var payload = JsonSerializer.Serialize(new
        {
            sale_id = sale.SaleId,
            created_at = sale.CreatedAt,
            payment_method = sale.PaymentMethod.ToString(),
            total_cents = sale.TotalCents,
            items = sale.Items.Select(x => new { product_id = x.ProductId, barcode = x.Barcode, description = x.Description, quantity = x.Quantity, price_cents = x.PriceCents, subtotal_cents = x.SubtotalCents })
        });

        await _salesRepository.SaveSaleWithOutboxAsync(sale, payload);
        CancelSale();
        var pending = await _outboxRepository.GetPendingCountAsync();
        StatusMessage = $"Venda finalizada. Outbox pendente: {pending}";
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
        FinalizeCommand.RaiseCanExecuteChanged();
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
