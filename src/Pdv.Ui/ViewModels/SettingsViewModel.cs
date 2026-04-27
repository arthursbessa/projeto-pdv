using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Printing;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IPdvSettingsRepository _settingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private bool _isBusy;
    private string _statusMessage = "Configure atalhos e impressao do PDV.";
    private bool _askPrinterBeforePrint = true;
    private string? _preferredPrinterName;
    private string _productTextCaseInput = "Original";
    private string _shortcutAddItem = "Enter";
    private string _shortcutFinalizeSale = "F2";
    private string _shortcutSearchProduct = "F3";
    private string _shortcutChangeQuantity = "F4";
    private string _shortcutChangePrice = "F5";
    private string _shortcutSelectCustomer = "F7";
    private string _shortcutReprintLastSale = "F8";
    private string _shortcutRemoveItem = "Delete";
    private string _shortcutCancelSale = "Escape";
    private string _defaultDiscountPercentInput = "0";

    public SettingsViewModel(IPdvSettingsRepository settingsRepository, IErrorFileLogger errorLogger)
    {
        _settingsRepository = settingsRepository;
        _errorLogger = errorLogger;
    }

    public ObservableCollection<string> AvailableShortcutKeys { get; } = new(ShortcutKeyHelper.AvailableKeys);
    public ObservableCollection<string> AvailablePrinters { get; } = [];

    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public bool AskPrinterBeforePrint { get => _askPrinterBeforePrint; set => SetField(ref _askPrinterBeforePrint, value); }
    public string? PreferredPrinterName { get => _preferredPrinterName; set => SetField(ref _preferredPrinterName, value); }
    public string ProductTextCaseInput { get => _productTextCaseInput; private set => SetField(ref _productTextCaseInput, value); }
    public string ShortcutAddItem { get => _shortcutAddItem; set => SetField(ref _shortcutAddItem, value); }
    public string ShortcutFinalizeSale { get => _shortcutFinalizeSale; set => SetField(ref _shortcutFinalizeSale, value); }
    public string ShortcutSearchProduct { get => _shortcutSearchProduct; set => SetField(ref _shortcutSearchProduct, value); }
    public string ShortcutChangeQuantity { get => _shortcutChangeQuantity; set => SetField(ref _shortcutChangeQuantity, value); }
    public string ShortcutChangePrice { get => _shortcutChangePrice; set => SetField(ref _shortcutChangePrice, value); }
    public string ShortcutSelectCustomer { get => _shortcutSelectCustomer; set => SetField(ref _shortcutSelectCustomer, value); }
    public string ShortcutReprintLastSale { get => _shortcutReprintLastSale; set => SetField(ref _shortcutReprintLastSale, value); }
    public string ShortcutRemoveItem { get => _shortcutRemoveItem; set => SetField(ref _shortcutRemoveItem, value); }
    public string ShortcutCancelSale { get => _shortcutCancelSale; set => SetField(ref _shortcutCancelSale, value); }
    public string DefaultDiscountPercentInput { get => _defaultDiscountPercentInput; set => SetField(ref _defaultDiscountPercentInput, value); }
    public async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetCurrentAsync();
        ProductTextCaseInput = ProductTextFormatter.ToDisplayLabel(settings.ProductTextCase);
        AskPrinterBeforePrint = settings.AskPrinterBeforePrint;
        PreferredPrinterName = settings.PreferredPrinterName;
        ShortcutAddItem = settings.ShortcutAddItem;
        ShortcutFinalizeSale = settings.ShortcutFinalizeSale;
        ShortcutSearchProduct = settings.ShortcutSearchProduct;
        ShortcutChangeQuantity = settings.ShortcutChangeQuantity;
        ShortcutChangePrice = settings.ShortcutChangePrice;
        ShortcutSelectCustomer = settings.ShortcutSelectCustomer;
        ShortcutReprintLastSale = settings.ShortcutReprintLastSale;
        ShortcutRemoveItem = settings.ShortcutRemoveItem;
        ShortcutCancelSale = settings.ShortcutCancelSale;
        DefaultDiscountPercentInput = settings.DefaultDiscountPercent.ToString("0.##");

        AvailablePrinters.Clear();
        try
        {
            foreach (var printer in new LocalPrintServer().GetPrintQueues().OrderBy(x => x.Name))
            {
                AvailablePrinters.Add(printer.Name);
            }
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao carregar impressoras do sistema", ex);
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (!TryBuildSettings(out var settings, out var errorMessage))
        {
            StatusMessage = errorMessage;
            return false;
        }

        IsBusy = true;
        try
        {
            await _settingsRepository.SaveAsync(settings);
            StatusMessage = "Configuracoes salvas com sucesso.";
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildSettings(out PdvSettings settings, out string errorMessage)
    {
        settings = new PdvSettings();
        errorMessage = string.Empty;

        var shortcutMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Adicionar item"] = ShortcutAddItem,
            ["Finalizar venda"] = ShortcutFinalizeSale,
            ["Buscar produto"] = ShortcutSearchProduct,
            ["Alterar quantidade"] = ShortcutChangeQuantity,
            ["Alterar preco"] = ShortcutChangePrice,
            ["Selecionar cliente"] = ShortcutSelectCustomer,
            ["Reimprimir ultima venda"] = ShortcutReprintLastSale,
            ["Remover item"] = ShortcutRemoveItem,
            ["Cancelar / voltar"] = ShortcutCancelSale
        };

        var duplicate = shortcutMap
            .GroupBy(x => ShortcutKeyHelper.NormalizeKeyName(x.Value), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

        if (duplicate is not null)
        {
            var conflictingActions = string.Join(", ", duplicate.Select(x => x.Key));
            errorMessage = $"Conflito de atalhos na tecla {duplicate.First().Value}: {conflictingActions}.";
            return false;
        }

        settings = new PdvSettings
        {
            ProductTextCase = ProductTextFormatter.ParseDisplayLabel(ProductTextCaseInput),
            AskPrinterBeforePrint = AskPrinterBeforePrint,
            PreferredPrinterName = string.IsNullOrWhiteSpace(PreferredPrinterName) ? null : PreferredPrinterName,
            ShortcutAddItem = ShortcutAddItem,
            ShortcutFinalizeSale = ShortcutFinalizeSale,
            ShortcutSearchProduct = ShortcutSearchProduct,
            ShortcutChangeQuantity = ShortcutChangeQuantity,
            ShortcutChangePrice = ShortcutChangePrice,
            ShortcutSelectCustomer = ShortcutSelectCustomer,
            ShortcutReprintLastSale = ShortcutReprintLastSale,
            ShortcutRemoveItem = ShortcutRemoveItem,
            ShortcutCancelSale = ShortcutCancelSale,
            DefaultDiscountPercent = 0m
        };

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
