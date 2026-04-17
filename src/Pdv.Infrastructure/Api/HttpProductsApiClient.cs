using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;

namespace Pdv.Infrastructure.Api;

public sealed class HttpProductsApiClient : IProductsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpProductsApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public Task<ProductAdminItem> CreateAsync(ProductAdminItem product, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, product, cancellationToken);

    public Task<ProductAdminItem> UpdateAsync(ProductAdminItem product, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Put, product, cancellationToken);

    public async Task UpdatePriceAsync(string productId, int priceCents, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de produtos do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-products";
        var payload = JsonSerializer.Serialize(new
        {
            id = productId,
            sale_price = priceCents / 100m
        });

        using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao atualizar preco do produto no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao atualizar preco do produto no PDV", exception);
            throw exception;
        }
    }

    private async Task<ProductAdminItem> SendAsync(HttpMethod method, ProductAdminItem product, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de produtos do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-products";
        var payload = JsonSerializer.Serialize(new
        {
            id = product.Id,
            name = TextNormalization.TrimToEmpty(product.Name),
            sku = TextNormalization.TrimToEmpty(product.Sku),
            barcode = TextNormalization.TrimToNull(product.Barcode),
            category_id = TextNormalization.TrimToNull(product.CategoryId),
            supplier_id = TextNormalization.TrimToNull(product.SupplierId),
            ncm = TextNormalization.TrimToNull(product.Ncm),
            cfop = TextNormalization.TrimToNull(product.Cfop),
            sale_price = product.PriceCents / 100m,
            cost_price = product.CostPriceCents / 100m,
            stock_quantity = product.StockQuantity,
            min_stock = product.MinStock,
            unit = TextNormalization.TrimToEmpty(product.Unit),
            is_active = product.Active
        });

        using var request = new HttpRequestMessage(method, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao salvar produto no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao salvar produto no PDV", exception);
            throw exception;
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("product", out var productElement))
        {
            throw new InvalidOperationException("Resposta invalida ao salvar produto no PDV.");
        }

        return new ProductAdminItem
        {
            Id = productElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : product.Id,
            Name = productElement.TryGetProperty("name", out var nameEl) ? TextNormalization.TrimToEmpty(nameEl.GetString()) : TextNormalization.TrimToEmpty(product.Name),
            Sku = productElement.TryGetProperty("sku", out var skuEl) ? TextNormalization.TrimToEmpty(skuEl.GetString()) : TextNormalization.TrimToEmpty(product.Sku),
            Barcode = productElement.TryGetProperty("barcode", out var barcodeEl) ? TextNormalization.TrimToEmpty(barcodeEl.GetString()) : string.Empty,
            CategoryId = productElement.TryGetProperty("category_id", out var categoryIdEl) ? TextNormalization.TrimToNull(categoryIdEl.GetString()) : product.CategoryId,
            SupplierId = productElement.TryGetProperty("supplier_id", out var supplierIdEl) ? TextNormalization.TrimToNull(supplierIdEl.GetString()) : product.SupplierId,
            Ncm = productElement.TryGetProperty("ncm", out var ncmEl) ? TextNormalization.TrimToNull(ncmEl.GetString()) : product.Ncm,
            Cfop = productElement.TryGetProperty("cfop", out var cfopEl) ? TextNormalization.TrimToNull(cfopEl.GetString()) : product.Cfop,
            PriceCents = productElement.TryGetProperty("sale_price", out var salePriceEl) && salePriceEl.TryGetDecimal(out var salePrice)
                ? (int)Math.Round(salePrice * 100m)
                : product.PriceCents,
            CostPriceCents = productElement.TryGetProperty("cost_price", out var costPriceEl) && costPriceEl.TryGetDecimal(out var costPrice)
                ? (int)Math.Round(costPrice * 100m)
                : product.CostPriceCents,
            StockQuantity = productElement.TryGetProperty("stock_quantity", out var stockEl) && stockEl.ValueKind == JsonValueKind.Number
                ? stockEl.GetInt32()
                : product.StockQuantity,
            MinStock = productElement.TryGetProperty("min_stock", out var minStockEl) && minStockEl.ValueKind == JsonValueKind.Number
                ? minStockEl.GetInt32()
                : product.MinStock,
            Unit = productElement.TryGetProperty("unit", out var unitEl) ? TextNormalization.TrimToEmpty(unitEl.GetString()) : TextNormalization.TrimToEmpty(product.Unit),
            Active = !productElement.TryGetProperty("is_active", out var activeEl) || activeEl.GetBoolean()
        };
    }

    public async Task DeleteAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de produtos do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-products";
        var payload = JsonSerializer.Serialize(new { id = productId });

        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao excluir produto no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao excluir produto no PDV", exception);
            throw exception;
        }
    }
}
