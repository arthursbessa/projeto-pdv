using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCashRegisterApiClient : ICashRegisterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpCashRegisterApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<string> OpenAsync(string operatorId, decimal amount, DateTimeOffset datetime, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            action = "open",
            operator_id = operatorId,
            amount = amount,
            datetime = datetime.ToString("O", CultureInfo.InvariantCulture)
        });

        var responseBody = await SendAsync(payload, cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("session", out var session) &&
            session.TryGetProperty("id", out var idElement))
        {
            return idElement.GetString() ?? throw new InvalidOperationException("Resposta inválida ao abrir caixa: id da sessão ausente.");
        }

        throw new InvalidOperationException("Resposta inválida ao abrir caixa: sessão ausente.");
    }

    public async Task CloseAsync(string sessionId, string operatorId, decimal amount, DateTimeOffset datetime, string? notes, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            action = "close",
            session_id = sessionId,
            operator_id = operatorId,
            amount = amount,
            datetime = datetime.ToString("O", CultureInfo.InvariantCulture),
            notes = notes
        });

        _ = await SendAsync(payload, cancellationToken);
    }

    public async Task RegisterWithdrawalAsync(string sessionId, string operatorId, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuração de integração de caixa incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-sangria";
        var payload = JsonSerializer.Serialize(new
        {
            session_id = sessionId,
            amount = amount,
            description,
            operator_id = operatorId
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var exception = new HttpRequestException(
                $"Falha ao registrar sangria em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {body}");
            _errorLogger.LogError("Falha na integracao de sangria do PDV", exception);
            throw exception;
        }
    }

    private async Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuração de integração de caixa incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-cash-register";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao integrar caixa em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {body}");
            _errorLogger.LogError("Falha na integracao de caixa do PDV", exception);
            throw exception;
        }

        return body;
    }
}
