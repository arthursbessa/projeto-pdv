using System.Net.Http.Headers;
using System.Text;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;

namespace Pdv.Infrastructure.Api;

public sealed class HttpSalesApiClient : ISalesApiClient
{
    private readonly HttpClient _httpClient;

    public HttpSalesApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
        if (!string.IsNullOrWhiteSpace(options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        }
    }

    public async Task SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/pdv/sales", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
