using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpReferenceDataApiClient : IReferenceDataApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpReferenceDataApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<ReferenceDataSnapshot> GetReferenceDataAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return new ReferenceDataSnapshot();
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-reference-data";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var exception = new HttpRequestException(
                $"Falha ao carregar dados de referencia do PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao consultar dados de referencia do PDV", exception);
            throw exception;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return new ReferenceDataSnapshot
        {
            Categories = ReadOptions(document.RootElement, "categories", true),
            Suppliers = ReadOptions(document.RootElement, "suppliers", false)
        };
    }

    private static IReadOnlyCollection<LookupOption> ReadOptions(JsonElement root, string propertyName, bool readParentId)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<LookupOption>();
        foreach (var item in property.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add(new LookupOption
            {
                Id = id,
                Name = name,
                ParentId = readParentId && item.TryGetProperty("parent_id", out var parentEl) ? parentEl.GetString() : null
            });
        }

        return list;
    }
}
