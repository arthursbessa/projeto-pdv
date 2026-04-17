using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;

namespace Pdv.Infrastructure.Api;

public sealed class HttpSuppliersApiClient : ISuppliersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpSuppliersApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<LookupOption> CreateAsync(SupplierCreateRequest requestModel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de fornecedores do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-suppliers";
        var payload = JsonSerializer.Serialize(new
        {
            name = TextNormalization.TrimToEmpty(requestModel.Name),
            cnpj = TextNormalization.FormatTaxId(requestModel.Cnpj),
            contact = TextNormalization.TrimToNull(requestModel.Contact),
            phone = TextNormalization.TrimToNull(requestModel.Phone),
            email = TextNormalization.TrimToNull(requestModel.Email),
            avg_delivery_days = requestModel.AvgDeliveryDays,
            notes = TextNormalization.TrimToNull(requestModel.Notes)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var exception = new HttpRequestException(
                $"Falha ao criar fornecedor no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao criar fornecedor no PDV", exception);
            throw exception;
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("supplier", out var supplierElement))
        {
            throw new InvalidOperationException("Resposta invalida ao criar fornecedor no PDV.");
        }

        return new LookupOption
        {
            Id = supplierElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
            Name = supplierElement.TryGetProperty("name", out var nameEl) ? TextNormalization.TrimToEmpty(nameEl.GetString()) : TextNormalization.TrimToEmpty(requestModel.Name)
        };
    }
}
