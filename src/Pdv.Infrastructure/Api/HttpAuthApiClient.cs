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

    public HttpAuthApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SupabaseBaseUrl) || string.IsNullOrWhiteSpace(_options.SupabaseAnonKey))
        {
            throw new InvalidOperationException("Configuração de autenticação Supabase incompleta.");
        }

        var endpoint = $"{_options.SupabaseBaseUrl.TrimEnd('/')}/auth/v1/token?grant_type=password";
        var payload = JsonSerializer.Serialize(new { email = username.Trim(), password });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("apikey", _options.SupabaseAnonKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("user", out var userElement))
        {
            return null;
        }

        var userId = userElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var email = userElement.TryGetProperty("email", out var emailElement) ? emailElement.GetString() ?? username.Trim() : username.Trim();
        string fullName = email;

        if (userElement.TryGetProperty("user_metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            if (metadata.TryGetProperty("full_name", out var fullNameElement) && !string.IsNullOrWhiteSpace(fullNameElement.GetString()))
            {
                fullName = fullNameElement.GetString()!;
            }
            else if (metadata.TryGetProperty("name", out var nameElement) && !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                fullName = nameElement.GetString()!;
            }
        }

        return new UserAccount
        {
            Id = userId,
            Username = email,
            FullName = fullName,
            PasswordHash = string.Empty,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
