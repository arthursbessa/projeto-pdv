using System.Net.Http.Headers;
using System.Text;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;

namespace Pdv.Infrastructure.Api;

public sealed class HttpRefundsApiClient : IRefundsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpRefundsApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task RegisterRefundAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de integracao de devolucoes incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-refunds";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var exception = new HttpRequestException(
                $"Falha ao registrar devolucao em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao registrar devolucao no PDV", exception);
            throw exception;
        }
    }
}
