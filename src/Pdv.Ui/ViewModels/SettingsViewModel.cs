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
    private string _statusMessage = "Configure atalhos, impressao, desconto default e exibicao de produtos.";
    private string _defaultDiscountPercentInput = "5";
    private bool _askPrinterBeforePrint = true;
    private string? _preferredPrinterName;
    private string _productTextCaseInput = "Original";
    private string _shortcutAddItem = "Enter";
    private string _shortcutFinalizeSale = "F2";
    private string _shortcutSearchProduct = "F3";
    private string _shortcutRemoveItem = "F4";
    private string _shortcutCancelSale = "Escape";

    public SettingsViewModel(IPdvSettingsRepository settingsRepository, IErrorFileLogger errorLogger)
    {
        _settingsRepository = settingsRepository;
        _errorLogger = errorLogger;
    }

    public ObservableCollection<string> AvailableShortcutKeys { get; } = new(ShortcutKeyHelper.AvailableKeys);
    public ObservableCollection<string> AvailablePrinters { get; } = [];
    public ObservableCollection<string> AvailableProductTextCases { get; } = new(["Original", "Maiusculas", "Minusculas"]);

    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string DefaultDiscountPercentInput { get => _defaultDiscountPercentInput; set => SetField(ref _defaultDiscountPercentInput, value); }
    public bool AskPrinterBeforePrint { get => _askPrinterBeforePrint; set => SetField(ref _askPrinterBeforePrint, value); }
    public string? PreferredPrinterName { get => _preferredPrinterName; set => SetField(ref _preferredPrinterName, value); }
    public string ProductTextCaseInput { get => _productTextCaseInput; set => SetField(ref _productTextCaseInput, value); }
    public string ShortcutAddItem { get => _shortcutAddItem; set => SetField(ref _shortcutAddItem, value); }
    public string ShortcutFinalizeSale { get => _shortcutFinalizeSale; set => SetField(ref _shortcutFinalizeSale, value); }
    public string ShortcutSearchProduct { get => _shortcutSearchProduct; set => SetField(ref _shortcutSearchProduct, value); }
    public string ShortcutRemoveItem { get => _shortcutRemoveItem; set => SetField(ref _shortcutRemoveItem, value); }
    public string ShortcutCancelSale { get => _shortcutCancelSale; set => SetField(ref _shortcutCancelSale, value); }

    public async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetCurrentAsync();
        DefaultDiscountPercentInput = settings.DefaultDiscountPercent.ToString("0.##");
        ProductTextCaseInput = ProductTextFormatter.ToDisplayLabel(settings.ProductTextCase);
        AskPrinterBeforePrint = settings.AskPrinterBeforePrint;
        PreferredPrinterName = settings.PreferredPrinterName;
        ShortcutAddItem = settings.ShortcutAddItem;
        ShortcutFinalizeSale = settings.ShortcutFinalizeSale;
        ShortcutSearchProduct = settings.ShortcutSearchProduct;
        ShortcutRemoveItem = settings.ShortcutRemoveItem;
        ShortcutCancelSale = settings.ShortcutCancelSale;

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
        if (!decimal.TryParse(DefaultDiscountPercentInput.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var discountPercent))
        {
            StatusMessage = "Informe um percentual de desconto valido.";
            return false;
        }

        var settings = new PdvSettings
        {
            DefaultDiscountPercent = Math.Clamp(discountPercent, 0m, 100m),
            ProductTextCase = ProductTextFormatter.ParseDisplayLabel(ProductTextCaseInput),
            AskPrinterBeforePrint = AskPrinterBeforePrint,
            PreferredPrinterName = string.IsNullOrWhiteSpace(PreferredPrinterName) ? null : PreferredPrinterName,
            ShortcutAddItem = ShortcutAddItem,
            ShortcutFinalizeSale = ShortcutFinalizeSale,
            ShortcutSearchProduct = ShortcutSearchProduct,
            ShortcutRemoveItem = ShortcutRemoveItem,
            ShortcutCancelSale = ShortcutCancelSale
        };

        await _settingsRepository.SaveAsync(settings);
        StatusMessage = "Configuracoes salvas com sucesso.";
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
