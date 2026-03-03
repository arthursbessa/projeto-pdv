using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;

    public HttpCatalogApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyCollection<ProductCacheItem>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return [];
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-catalog";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-pdv-token", _options.TerminalToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var list = new List<ProductCacheItem>();
        var catalogRoot = document.RootElement;

        if (catalogRoot.ValueKind == JsonValueKind.Object
            && catalogRoot.TryGetProperty("catalog", out var catalogElement)
            && catalogElement.ValueKind == JsonValueKind.Array)
        {
            catalogRoot = catalogElement;
        }

        if (catalogRoot.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in catalogRoot.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var barcode = item.TryGetProperty("barcode", out var barcodeEl) ? barcodeEl.GetString() : null;
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var priceDecimal = GetDecimal(item, "price", "sale_price", "promo_price");
            var active = item.TryGetProperty("is_active", out var activeEl) ? activeEl.GetBoolean() : true;
            var now = DateTimeOffset.UtcNow;

            list.Add(new ProductCacheItem
            {
                ProductId = id,
                Barcode = barcode,
                Description = name,
                PriceCents = (int)Math.Round(priceDecimal * 100m, MidpointRounding.AwayFromZero),
                Active = active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return list;
    }

    private static decimal GetDecimal(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var value))
            {
                return value;
            }

            if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
        }

        return 0m;
    }
}
