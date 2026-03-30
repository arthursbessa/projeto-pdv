using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpSalesApiClient : ISalesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpSalesApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<SaleSyncResult> SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de integracao de vendas incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-sales";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao enviar venda para '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao enviar venda para o PDV", exception);
            throw exception;
        }

        using var document = JsonDocument.Parse(responseBody);
        return new SaleSyncResult
        {
            RemoteSaleId = document.RootElement.TryGetProperty("sale_id", out var saleIdElement)
                ? saleIdElement.GetString() ?? string.Empty
                : string.Empty,
            SaleNumber = document.RootElement.TryGetProperty("sale_number", out var saleNumberElement) && saleNumberElement.ValueKind == JsonValueKind.Number
                ? saleNumberElement.GetInt32()
                : null
        };
    }
}
