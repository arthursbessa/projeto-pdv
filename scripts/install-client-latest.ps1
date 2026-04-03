param(
    [string]$RepositoryOwner = "arthursbessa",
    [string]$RepositoryName = "projeto-pdv",
    [string]$PackagePrefix = "PDV-Client",
    [string]$InstallDir = "$env:ProgramFiles\PDV-Client"
)

$ErrorActionPreference = "Stop"

if (-not ("System.Net.Http.HttpClient" -as [type])) {
    try {
        Add-Type -AssemblyName "System.Net.Http"
    }
    catch {
    }
}

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
}
catch {
}

# Configuracao para execucao por clique duplo
$DefaultRepositoryOwner = "arthursbessa"
$DefaultRepositoryName = "projeto-pdv"
$DefaultPackagePrefix = "PDV-Client"
$DefaultInstallDir = "$env:ProgramFiles\PDV-Client"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-InteractiveParameters {
    if ([string]::IsNullOrWhiteSpace($script:RepositoryOwner)) {
        $script:RepositoryOwner = $DefaultRepositoryOwner
    }

    if ([string]::IsNullOrWhiteSpace($script:RepositoryName)) {
        $script:RepositoryName = $DefaultRepositoryName
    }

    if ([string]::IsNullOrWhiteSpace($script:PackagePrefix)) {
        $script:PackagePrefix = $DefaultPackagePrefix
    }

    if ([string]::IsNullOrWhiteSpace($script:InstallDir)) {
        $script:InstallDir = $DefaultInstallDir
    }

    $customInstallDir = Read-Host "Diretorio de instalacao atual: $($script:InstallDir). Pressione ENTER para manter"
    if (-not [string]::IsNullOrWhiteSpace($customInstallDir)) {
        $script:InstallDir = $customInstallDir.Trim()
    }
}

function Test-IsAdmin {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Elevated {
    if (Test-IsAdmin) {
        return
    }

    $argumentList = @(
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`"",
        "-RepositoryOwner", "`"$RepositoryOwner`"",
        "-RepositoryName", "`"$RepositoryName`"",
        "-PackagePrefix", "`"$PackagePrefix`"",
        "-InstallDir", "`"$InstallDir`""
    ) -join ' '

    Start-Process -FilePath "powershell.exe" -ArgumentList $argumentList -Verb RunAs | Out-Null
    exit
}

function New-GitHubClient {
    if (-not ("System.Net.Http.HttpClient" -as [type])) {
        throw "Nao foi possivel carregar o assembly System.Net.Http neste PowerShell."
    }

    $client = New-Object System.Net.Http.HttpClient
    $client.DefaultRequestHeaders.UserAgent.ParseAdd("pdv-client-installer")
    $client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json")
    return $client
}

function Get-LatestRelease {
    param(
        [string]$Owner,
        [string]$Name
    )

    $client = New-GitHubClient
    try {
        $url = "https://api.github.com/repos/$Owner/$Name/releases/latest"
        $json = $client.GetStringAsync($url).GetAwaiter().GetResult()
        return $json | ConvertFrom-Json
    }
    finally {
        $client.Dispose()
    }
}

function Find-PackageAsset {
    param(
        [object]$Release,
        [string]$Prefix
    )

    $assets = @($Release.assets)
    $zipAssets = @($assets | Where-Object { $_.name -like "*.zip" })
    if ($zipAssets.Count -eq 0) {
        throw "A release '$($Release.tag_name)' nao possui nenhum arquivo .zip anexado."
    }

    $candidatePrefixes = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
        $candidatePrefixes.Add($Prefix)
    }

    if ($Prefix -eq "PDV-Client") {
        $candidatePrefixes.Add("PDV-Cliente")
    }
    elseif ($Prefix -eq "PDV-Cliente") {
        $candidatePrefixes.Add("PDV-Client")
    }

    foreach ($candidatePrefix in $candidatePrefixes) {
        $expectedName = "$candidatePrefix-$($Release.tag_name).zip"
        foreach ($asset in $zipAssets) {
            if ($asset.name -eq $expectedName) {
                return $asset
            }
        }
    }

    foreach ($candidatePrefix in $candidatePrefixes) {
        foreach ($asset in $zipAssets) {
            if ($asset.name -like "$candidatePrefix-*.zip") {
                return $asset
            }
        }
    }

    foreach ($asset in $zipAssets) {
        if ($asset.name -like "*$($Release.tag_name)*") {
            return $asset
        }
    }

    if ($zipAssets.Count -eq 1) {
        return $zipAssets[0]
    }

    $availableAssets = ($zipAssets | ForEach-Object { $_.name }) -join ", "
    throw "Nao foi encontrado um asset compativel para a release '$($Release.tag_name)'. Zips disponiveis: $availableAssets"
}

function Download-File {
    param(
        [string]$Url,
        [string]$DestinationPath
    )

    $client = New-GitHubClient
    try {
        $response = $client.GetAsync($Url).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode()

        $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        try {
            $fileStream = [System.IO.File]::Create($DestinationPath)
            try {
                $stream.CopyTo($fileStream)
            }
            finally {
                $fileStream.Dispose()
            }
        }
        finally {
            $stream.Dispose()
            $response.Dispose()
        }
    }
    finally {
        $client.Dispose()
    }
}

function Copy-FolderContent {
    param(
        [string]$Source,
        [string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        $targetPath = Join-Path $Destination $_.Name

        if ($_.PSIsContainer) {
            New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            Copy-FolderContent -Source $_.FullName -Destination $targetPath
            return
        }

        if ($_.Name -in @("appsettings.json", "appsettings.local.json") -and (Test-Path $targetPath)) {
            return
        }

        Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
    }
}

Resolve-InteractiveParameters
Ensure-Elevated

Write-Step "Consultando ultima release no GitHub"
$release = Get-LatestRelease -Owner $RepositoryOwner -Name $RepositoryName
$asset = Find-PackageAsset -Release $release -Prefix $PackagePrefix

$tempRoot = Join-Path $env:TEMP ("pdv-client-install-" + [guid]::NewGuid().ToString("N"))
$extractDir = Join-Path $tempRoot "extract"
$zipPath = Join-Path $tempRoot $asset.name
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

try {
    Write-Step "Baixando pacote $($asset.name)"
    Download-File -Url $asset.browser_download_url -DestinationPath $zipPath

    Write-Step "Extraindo arquivos"
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

    Write-Step "Preparando pasta do sistema"
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Get-ChildItem -LiteralPath $InstallDir -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -in @("logs", "appsettings.json", "appsettings.local.json")) {
            return
        }

        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $InstallDir "logs") -Force | Out-Null

    Write-Step "Copiando arquivos para o cliente"
    Copy-FolderContent -Source $extractDir -Destination $InstallDir

    $exePath = Join-Path $InstallDir "Pdv.Ui.exe"
    if (-not (Test-Path $exePath)) {
        throw "Instalacao concluida sem localizar o executavel em '$exePath'."
    }

    Write-Host ""
    Write-Host "Implantacao concluida com sucesso." -ForegroundColor Green
    Write-Host "Versao:        $($release.tag_name)"
    Write-Host "Pacote:        $($asset.name)"
    Write-Host "Release URL:   $($release.html_url)"
    Write-Host "Instalacao:    $InstallDir"
    Write-Host "Executavel:    $exePath"
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
