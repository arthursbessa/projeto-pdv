using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Pdv.Application.Configuration;

namespace Pdv.Ui.Services;

public sealed class GitHubReleaseUpdateService
{
    private const string GitHubApiBaseUrl = "https://api.github.com/repos";

    private readonly PdvOptions _options;
    private readonly AppRuntimeInfoService _runtimeInfo;
    private readonly IErrorFileLogger _errorLogger;

    public GitHubReleaseUpdateService(
        PdvOptions options,
        AppRuntimeInfoService runtimeInfo,
        IErrorFileLogger errorLogger)
    {
        _options = options;
        _runtimeInfo = runtimeInfo;
        _errorLogger = errorLogger;
    }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (_runtimeInfo.IsDevelopment || !_options.EnableAutoUpdateCheck)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.UpdateRepositoryOwner) ||
            string.IsNullOrWhiteSpace(_options.UpdateRepositoryName))
        {
            return null;
        }

        try
        {
            using var httpClient = CreateHttpClient();
            using var response = await httpClient.GetAsync(
                $"{GitHubApiBaseUrl}/{_options.UpdateRepositoryOwner}/{_options.UpdateRepositoryName}/releases/latest",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                return null;
            }

            var latestVersionTag = AppRuntimeInfoService.NormalizeVersionTag(tagElement.GetString());
            if (string.IsNullOrWhiteSpace(latestVersionTag) || !IsNewerVersion(latestVersionTag, _runtimeInfo.VersionTag))
            {
                return null;
            }

            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString() ?? string.Empty
                : string.Empty;

            var asset = FindReleaseAsset(root, latestVersionTag);
            if (asset is null)
            {
                return null;
            }

            return asset with { VersionTag = latestVersionTag, ReleaseUrl = releaseUrl };
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao verificar atualizacoes do PDV", ex);
            return null;
        }
    }

    public async Task<bool> TryStartUpdateAsync(AppUpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        if (_runtimeInfo.IsDevelopment || string.IsNullOrWhiteSpace(_runtimeInfo.ExecutablePath))
        {
            return false;
        }

        try
        {
            var updaterRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDV-Client",
                "updater");

            Directory.CreateDirectory(updaterRoot);

            var downloadsDirectory = Path.Combine(updaterRoot, "downloads");
            Directory.CreateDirectory(downloadsDirectory);

            var zipPath = Path.Combine(downloadsDirectory, updateInfo.AssetName);
            await DownloadFileAsync(updateInfo.DownloadUrl, zipPath, cancellationToken);

            var scriptPath = Path.Combine(updaterRoot, "apply-update.ps1");
            await File.WriteAllTextAsync(scriptPath, BuildUpdaterScript(), Encoding.UTF8, cancellationToken);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                    $"-ZipPath \"{zipPath}\" " +
                    $"-InstallDir \"{_runtimeInfo.InstallDirectory}\" " +
                    $"-ExePath \"{_runtimeInfo.ExecutablePath}\" " +
                    $"-CurrentVersion \"{_runtimeInfo.VersionTag}\" " +
                    $"-TargetVersion \"{updateInfo.VersionTag}\" " +
                    $"-ProcessId {Environment.ProcessId}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            });

            return process is not null;
        }
        catch (Exception ex)
        {
            _errorLogger.LogError("Falha ao iniciar atualizacao automatica do PDV", ex);
            return false;
        }
    }

    private async Task DownloadFileAsync(string downloadUrl, string destinationPath, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(fileStream, cancellationToken);
    }

    private HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("projeto-pdv-updater");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return httpClient;
    }

    private AppUpdateInfo? FindReleaseAsset(JsonElement root, string latestVersionTag)
    {
        if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var expectedAssetName = $"{_options.UpdatePackagePrefix}-{latestVersionTag}.zip";
        AppUpdateInfo? fallback = null;

        foreach (var asset in assetsElement.EnumerateArray())
        {
            var assetName = asset.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            var downloadUrl = asset.TryGetProperty("browser_download_url", out var downloadElement)
                ? downloadElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            if (!assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var updateInfo = new AppUpdateInfo(latestVersionTag, string.Empty, assetName, downloadUrl);
            if (assetName.Equals(expectedAssetName, StringComparison.OrdinalIgnoreCase))
            {
                return updateInfo;
            }

            fallback ??= updateInfo;
        }

        return fallback;
    }

    private static bool IsNewerVersion(string latestVersionTag, string currentVersionTag)
    {
        var latestVersion = ParseVersion(latestVersionTag);
        var currentVersion = ParseVersion(currentVersionTag);

        if (latestVersion is null || currentVersion is null)
        {
            return !string.Equals(latestVersionTag, currentVersionTag, StringComparison.OrdinalIgnoreCase);
        }

        return latestVersion > currentVersion;
    }

    private static Version? ParseVersion(string versionTag)
    {
        var normalized = AppRuntimeInfoService.NormalizeVersionTag(versionTag)?
            .TrimStart('v', 'V');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var pieces = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0 || pieces.Length > 4)
        {
            return null;
        }

        var paddedPieces = pieces.Concat(Enumerable.Repeat("0", 4 - pieces.Length));
        return Version.TryParse(string.Join('.', paddedPieces), out var version)
            ? version
            : null;
    }

    private static string BuildUpdaterScript() =>
        """
        param(
            [Parameter(Mandatory = $true)][string]$ZipPath,
            [Parameter(Mandatory = $true)][string]$InstallDir,
            [Parameter(Mandatory = $true)][string]$ExePath,
            [Parameter(Mandatory = $true)][string]$CurrentVersion,
            [Parameter(Mandatory = $true)][string]$TargetVersion,
            [Parameter(Mandatory = $true)][int]$ProcessId
        )

        $ErrorActionPreference = "Stop"

        function Copy-FolderContent {
            param(
                [string]$Source,
                [string]$Destination
            )

            Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
                $sourcePath = $_.FullName
                $targetPath = Join-Path $Destination $_.Name

                if ($_.PSIsContainer) {
                    if ($_.Name -in @("data", "logs", "updates")) {
                        return
                    }

                    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
                    Copy-FolderContent -Source $sourcePath -Destination $targetPath
                    return
                }

                if ($_.Name -in @("appsettings.json", "appsettings.local.json") -and (Test-Path $targetPath)) {
                    return
                }

                Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
            }
        }

        function Resolve-ExtractedContentRoot {
            param([string]$ExtractDir)

            $entries = @(Get-ChildItem -LiteralPath $ExtractDir -Force)
            if ($entries.Count -eq 1 -and $entries[0].PSIsContainer) {
                return $entries[0].FullName
            }

            return $ExtractDir
        }

        Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue

        $extractDir = Join-Path ([System.IO.Path]::GetTempPath()) ("pdv-update-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

        Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractDir -Force
        $contentRoot = Resolve-ExtractedContentRoot -ExtractDir $extractDir

        Get-ChildItem -LiteralPath $InstallDir -Force | ForEach-Object {
            if ($_.Name -in @("logs", "updates", "appsettings.json", "appsettings.local.json")) {
                return
            }

            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }

        New-Item -ItemType Directory -Path (Join-Path $InstallDir "logs") -Force | Out-Null
        Copy-FolderContent -Source $contentRoot -Destination $InstallDir

        $newExePath = Join-Path $InstallDir "Pdv.Ui.exe"
        if (-not (Test-Path $newExePath)) {
            throw "Atualizacao concluida sem localizar o executavel em '$newExePath'."
        }

        Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue

        Start-Process -FilePath $newExePath
        """;
}
