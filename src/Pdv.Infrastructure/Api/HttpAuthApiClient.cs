using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpAuthApiClient : IAuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;
    private static readonly string[] AuthFunctionNames = ["pdv-auth"];

    public HttpAuthApiClient(HttpClient httpClient, PdvOptions options, IErrorLogger errorLogger)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
    }

    public async Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            throw new InvalidOperationException("Configuração de autenticação PDV incompleta.");
        }

        var normalizedUsername = username.Trim();
        var payload = JsonSerializer.Serialize(new { username = normalizedUsername, password });

        HttpResponseMessage? response = null;
        foreach (var authFunctionName in AuthFunctionNames)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildAuthEndpoint(authFunctionName))
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            PdvApiRequestHeaders.Apply(request, _options);

            response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                break;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _errorLogger.LogError(
                    "Falha na autenticacao remota do PDV",
                    new HttpRequestException(
                        $"Endpoint '{request.RequestUri}' retornou {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}"));
                response.Dispose();
                return null;
            }

            response.Dispose();
            response = null;
        }

        if (response is null)
        {
            _errorLogger.LogError(
                "Nenhum endpoint de autenticacao do PDV respondeu com sucesso",
                new InvalidOperationException($"Endpoints tentados: {string.Join(", ", AuthFunctionNames)}."));
            return null;
        }

        using (response)
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("success", out var successElement)
                || successElement.ValueKind != JsonValueKind.True
                || !document.RootElement.TryGetProperty("user", out var userElement))
            {
                _errorLogger.LogError(
                    "Resposta invalida na autenticacao remota do PDV",
                    new InvalidOperationException($"Endpoint '{response.RequestMessage?.RequestUri}' retornou payload sem os campos esperados."));
                return null;
            }

            var userId = userElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var accountUsername = normalizedUsername;
            string fullName = normalizedUsername;

            if (userElement.TryGetProperty("display_name", out var displayNameElement)
                && !string.IsNullOrWhiteSpace(displayNameElement.GetString()))
            {
                fullName = displayNameElement.GetString()!;
            }
            else if (userElement.TryGetProperty("username", out var usernameElement)
                && !string.IsNullOrWhiteSpace(usernameElement.GetString()))
            {
                fullName = usernameElement.GetString()!;
            }
            else if (userElement.TryGetProperty("email", out var emailElement)
                && !string.IsNullOrWhiteSpace(emailElement.GetString()))
            {
                fullName = emailElement.GetString()!;
            }

            return new UserAccount
            {
                Id = userId,
                Username = accountUsername,
                FullName = fullName,
                PasswordHash = string.Empty,
                Active = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private string BuildAuthEndpoint(string functionName)
    {
        var baseUrl = _options.FunctionsBaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith($"/{functionName}", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        return $"{baseUrl}/{functionName}";
    }
}
