using System.Net.Http.Headers;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Api;

public sealed class HttpStoreSettingsApiClient : IStoreSettingsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;

    public HttpStoreSettingsApiClient(HttpClient httpClient, PdvOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<StoreSettings?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return null;
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-store-settings";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("x-pdv-token", _options.TerminalToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("settings", out var settings))
        {
            return null;
        }

        var logoUrl = settings.TryGetProperty("logo_url", out var logoEl) ? logoEl.GetString() ?? string.Empty : string.Empty;
        var logoLocalPath = await DownloadLogoAsync(logoUrl, cancellationToken);

        return new StoreSettings
        {
            StoreName = settings.TryGetProperty("store_name", out var nameEl) ? nameEl.GetString() ?? "LOJA" : "LOJA",
            Cnpj = settings.TryGetProperty("cnpj", out var cnpjEl) ? cnpjEl.GetString() ?? string.Empty : string.Empty,
            Address = settings.TryGetProperty("address", out var addressEl) ? addressEl.GetString() ?? string.Empty : string.Empty,
            Timezone = settings.TryGetProperty("timezone", out var tzEl) ? tzEl.GetString() ?? "America/Sao_Paulo" : "America/Sao_Paulo",
            Currency = settings.TryGetProperty("currency", out var currencyEl) ? currencyEl.GetString() ?? "BRL" : "BRL",
            LogoUrl = logoUrl,
            LogoLocalPath = logoLocalPath,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<string> DownloadLogoAsync(string logoUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        try
        {
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var directory = Path.Combine(AppContext.BaseDirectory, "data", "assets");
            Directory.CreateDirectory(directory);
            var fullPath = Path.Combine(directory, $"store-logo{extension}");

            var bytes = await _httpClient.GetByteArrayAsync(uri, cancellationToken);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
            return fullPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
