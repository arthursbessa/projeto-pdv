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

    public HttpCashRegisterApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _options = options;
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

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-pdv-token", _options.TerminalToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
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

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-pdv-token", _options.TerminalToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }
}
