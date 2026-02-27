using System.Net.Http.Headers;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _httpClient;

    public HttpCatalogApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
        if (!string.IsNullOrWhiteSpace(options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        }
    }

    public async Task<IReadOnlyCollection<ProductCacheItem>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/pdv/catalog", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var products = await JsonSerializer.DeserializeAsync<List<ProductCacheItem>>(stream, cancellationToken: cancellationToken);
        return products ?? [];
    }
}
