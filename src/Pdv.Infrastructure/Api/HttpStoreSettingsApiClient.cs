using System.Net.Http.Headers;
using System.Text.Json;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;

namespace Pdv.Infrastructure.Api;

public sealed class HttpStoreSettingsApiClient : IStoreSettingsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PdvOptions _options;
    private readonly IErrorLogger _errorLogger;
    private readonly AppStoragePaths _storagePaths;

    public HttpStoreSettingsApiClient(
        HttpClient httpClient,
        PdvOptions options,
        IErrorLogger errorLogger,
        AppStoragePaths storagePaths)
    {
        _httpClient = httpClient;
        _options = options;
        _errorLogger = errorLogger;
        _storagePaths = storagePaths;
    }

    public async Task<StoreSettings?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FunctionsBaseUrl) || string.IsNullOrWhiteSpace(_options.TerminalToken))
        {
            return null;
        }

        var endpoint = $"{_options.FunctionsBaseUrl.TrimEnd('/')}/pdv-store-settings";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        PdvApiRequestHeaders.Apply(request, _options);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _errorLogger.LogError(
                "Falha ao consultar configuracoes da loja no PDV",
                new HttpRequestException(
                    $"Endpoint '{endpoint}' retornou {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {responseBody}"));
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("settings", out var settings))
        {
            _errorLogger.LogError(
                "Resposta invalida nas configuracoes da loja do PDV",
                new InvalidOperationException($"Endpoint '{endpoint}' nao retornou o objeto 'settings'."));
            return null;
        }

        var logoUrl = settings.TryGetProperty("logo_url", out var logoEl) ? logoEl.GetString() ?? string.Empty : string.Empty;
        var logoLocalPath = await DownloadLogoAsync(logoUrl, cancellationToken);

        return new StoreSettings
        {
            TerminalName = doc.RootElement.TryGetProperty("terminal", out var terminalEl) ? TextNormalization.TrimToEmpty(terminalEl.GetString()) : string.Empty,
            StoreName = settings.TryGetProperty("store_name", out var nameEl) ? TextNormalization.TrimToEmpty(nameEl.GetString()) : "LOJA",
            Cnpj = settings.TryGetProperty("cnpj", out var cnpjEl) ? TextNormalization.FormatTaxIdPartial(cnpjEl.GetString()) : string.Empty,
            Address = settings.TryGetProperty("address", out var addressEl) ? TextNormalization.TrimToEmpty(addressEl.GetString()) : string.Empty,
            Timezone = settings.TryGetProperty("timezone", out var tzEl) ? TextNormalization.TrimToEmpty(tzEl.GetString()) : "America/Sao_Paulo",
            Currency = settings.TryGetProperty("currency", out var currencyEl) ? TextNormalization.TrimToEmpty(currencyEl.GetString()) : "BRL",
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

            var directory = _storagePaths.AssetsDirectory;
            Directory.CreateDirectory(directory);
            var fileName = $"store-logo-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
            var fullPath = Path.Combine(directory, fileName);

            var bytes = await _httpClient.GetByteArrayAsync(uri, cancellationToken);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
            CleanupOldLogoFiles(directory, fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError($"Falha ao baixar logo da loja em '{logoUrl}'", ex);
            return string.Empty;
        }
    }

    private static void CleanupOldLogoFiles(string directory, string currentFilePath)
    {
        try
        {
            foreach (var filePath in Directory.GetFiles(directory, "store-logo-*"))
            {
                if (string.Equals(filePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}
