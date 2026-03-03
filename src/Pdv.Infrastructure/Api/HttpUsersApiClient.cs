using System.Net.Http.Headers;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Repositories;

namespace Pdv.Infrastructure.Api;

public sealed class HttpUsersApiClient : IUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;

    public HttpUsersApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyCollection<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return [];
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-users";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("x-pdv-token", _options.TerminalToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("users", out var usersElement) || usersElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<UserAccount>();
        foreach (var element in usersElement.EnumerateArray())
        {
            var username = element.TryGetProperty("email", out var emailEl)
                ? emailEl.GetString() ?? string.Empty
                : element.TryGetProperty("username", out var usernameEl)
                    ? usernameEl.GetString() ?? string.Empty
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var hash = element.TryGetProperty("password_hash", out var hashEl)
                ? hashEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(hash) && element.TryGetProperty("password", out var passwordEl))
            {
                hash = UserRepository.HashPassword(passwordEl.GetString() ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(hash))
            {
                continue;
            }

            var fullName = element.TryGetProperty("display_name", out var displayNameEl)
                ? displayNameEl.GetString() ?? username
                : element.TryGetProperty("full_name", out var fullNameEl)
                    ? fullNameEl.GetString() ?? username
                    : username;

            result.Add(new UserAccount
            {
                Id = element.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                Username = username.Trim(),
                FullName = fullName.Trim(),
                PasswordHash = hash.Trim(),
                Active = !element.TryGetProperty("active", out var activeEl) || activeEl.ValueKind != JsonValueKind.False,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        return result;
    }
}
