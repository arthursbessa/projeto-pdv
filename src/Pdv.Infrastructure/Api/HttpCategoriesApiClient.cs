using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpCategoriesApiClient : ICategoriesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;

    public HttpCategoriesApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<LookupOption> CreateAsync(string name, string? parentId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuracao de categorias do PDV incompleta.");
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-categories";
        var payload = JsonSerializer.Serialize(new
        {
            name,
            parent_id = string.IsNullOrWhiteSpace(parentId) ? null : parentId
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
                $"Falha ao criar categoria no PDV em '{endpoint}'. Status: {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}");
            _errorLogger.LogError("Falha ao criar categoria no PDV", exception);
            throw exception;
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("category", out var categoryElement))
        {
            throw new InvalidOperationException("Resposta invalida ao criar categoria no PDV.");
        }

        return new LookupOption
        {
            Id = categoryElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
            Name = categoryElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? name : name,
            ParentId = categoryElement.TryGetProperty("parent_id", out var parentEl) ? parentEl.GetString() : parentId
        };
    }
}
