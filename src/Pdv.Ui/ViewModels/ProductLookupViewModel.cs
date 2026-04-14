using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Ui.Formatting;
using Pdv.Ui.Services;

namespace Pdv.Ui.ViewModels;

public sealed class ProductLookupViewModel : INotifyPropertyChanged
{
    private readonly ICatalogApiClient _catalogApiClient;
    private readonly IProductCacheRepository _productCacheRepository;
    private readonly IPdvSettingsRepository _pdvSettingsRepository;
    private readonly IErrorFileLogger _errorLogger;
    private string _query = string.Empty;
    private ProductLookupItemViewModel? _selectedProduct;
    private string _statusMessage = "Carregando produtos...";
    private bool _isBusy;
    private ProductTextCaseMode _productTextCase = ProductTextCaseMode.Original;

    public ProductLookupViewModel(
        ICatalogApiClient catalogApiClient,
        IProductCacheRepository productCacheRepository,
        IPdvSettingsRepository pdvSettingsRepository,
        IErrorFileLogger errorLogger)
    {
        _catalogApiClient = catalogApiClient;
        _productCacheRepository = productCacheRepository;
        _pdvSettingsRepository = pdvSettingsRepository;
        _errorLogger = errorLogger;
    }

    public ObservableCollection<ProductLookupItemViewModel> Products { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value))
            {
                _ = SearchAsync();
            }
        }
    }

    public ProductLookupItemViewModel? SelectedProduct
    {
        get => _selectedProduct;
        set => SetField(ref _selectedProduct, value);
    }

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

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var settings = await _pdvSettingsRepository.GetCurrentAsync();
            _productTextCase = settings.ProductTextCase;

            try
            {
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

                StatusMessage = $"{remoteProducts.Count} produto(s) sincronizado(s) da API.";
            }
            catch (Exception ex)
            {
                _errorLogger.LogError("Falha ao sincronizar produtos na busca manual", ex);
                StatusMessage = "Sem conexao com a API. Exibindo catalogo local.";
            }

            await SearchAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAsync()
    {
        var products = await _productCacheRepository.SearchAsync(Query);
        Products.Clear();

        foreach (var product in products.Where(p => p.Active))
        {
                Products.Add(new ProductLookupItemViewModel
                {
                    Id = product.ProductId,
                    Barcode = product.Barcode,
                    Description = ProductTextFormatter.Format(product.Description, _productTextCase),
                    PriceCents = product.PriceCents,
                    PriceFormatted = MoneyFormatter.FormatFromCents(product.PriceCents)
                });
            }

        StatusMessage = Products.Count == 0
            ? "Nenhum produto encontrado."
            : $"{Products.Count} produto(s) disponivel(is).";
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
