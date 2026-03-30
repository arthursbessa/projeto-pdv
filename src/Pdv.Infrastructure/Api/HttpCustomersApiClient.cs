using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCustomersApiClient : ICustomersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpCustomersApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<IReadOnlyCollection<CustomerRecord>> GetCustomersAsync(string? search = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return [];
        }

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        query.Add($"limit={limit}");
        var queryString = string.Join("&", query);
        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-customers";
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            endpoint += $"?{queryString}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var exception = new HttpRequestException(
                $"Falha ao consultar clientes do PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao consultar clientes do PDV", exception);
            throw exception;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("customers", out var customersElement) || customersElement.ValueKind != JsonValueKind.Array)
        {
            _errorLogger.LogError(
                "Resposta invalida na consulta de clientes do PDV",
                new InvalidOperationException($"Endpoint '{endpoint}' nao retornou uma lista em 'customers'."));
            return [];
        }

        var result = new List<CustomerRecord>();
        foreach (var customerElement in customersElement.EnumerateArray())
        {
            result.Add(ParseCustomer(customerElement));
        }

        return result;
    }

    public async Task<CustomerRecord> CreateCustomerAsync(CustomerRecord customer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de clientes do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-customers";
        var payload = JsonSerializer.Serialize(new
        {
            name = customer.Name,
            cpf = customer.Cpf,
            phone = customer.Phone,
            email = customer.Email,
            address = customer.Address,
            notes = customer.Notes
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            var exception = new HttpRequestException(
                $"Falha ao criar cliente no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao criar cliente no PDV", exception);
            throw exception;
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("customer", out var customerElement))
        {
            throw new InvalidOperationException("Resposta invalida ao criar cliente: objeto 'customer' ausente.");
        }

        return ParseCustomer(customerElement);
    }

    private static CustomerRecord ParseCustomer(JsonElement element)
    {
        return new CustomerRecord
        {
            Id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
            Name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
            Cpf = element.TryGetProperty("cpf", out var cpfEl) ? cpfEl.GetString() ?? string.Empty : string.Empty,
            Phone = element.TryGetProperty("phone", out var phoneEl) ? phoneEl.GetString() ?? string.Empty : string.Empty,
            Email = element.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? string.Empty : string.Empty,
            Address = element.TryGetProperty("address", out var addressEl) ? addressEl.GetString() ?? string.Empty : string.Empty,
            Notes = element.TryGetProperty("notes", out var notesEl) ? notesEl.GetString() ?? string.Empty : string.Empty,
            Active = true,
            CreatedAt = element.TryGetProperty("created_at", out var createdAtEl) && createdAtEl.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(createdAtEl.GetString()!)
                : DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
