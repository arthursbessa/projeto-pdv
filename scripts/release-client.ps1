param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string]$PackagePrefix = "PDV-Client",
    [switch]$SkipTests,
    [switch]$SkipGit,
    [switch]$SkipRelease,
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"

# Configuracao para execucao por clique duplo
# Se quiser, preencha aqui e execute pelo arquivo .cmd sem passar nada no terminal.
$DefaultVersion = ""
$DefaultRuntime = "win-x64"
$DefaultConfiguration = "Release"
$DefaultOutputRoot = ""

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-RepositoryRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw "Nao foi possivel localizar a raiz do repositorio Git."
    }

    return $root.Trim()
}

function Get-CurrentBranch {
    $branch = git branch --show-current 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
        throw "Nao foi possivel identificar a branch atual."
    }

    return $branch.Trim()
}

function Get-OriginRepository {
    $originUrl = git remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        throw "Nao foi possivel identificar o remote 'origin'."
    }

    $originUrl = $originUrl.Trim()

    if ($originUrl -match 'github\.com[:/](.+?)/(.+?)(\.git)?$') {
        return @{
            Owner = $matches[1]
            Name = $matches[2]
        }
    }

    throw "O remote '$originUrl' nao parece ser um repositorio GitHub suportado por este script."
}

function Assert-CleanWorktree {
    param([string]$RepoRoot)

    $status = git -C $RepoRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel verificar o estado do Git."
    }

    if (-not [string]::IsNullOrWhiteSpace(($status | Out-String))) {
        throw "Existem alteracoes locais nao commitadas. Faca commit/stash ou rode com -AllowDirty."
    }
}

function Test-GitTagExists {
    param(
        [string]$RepoRoot,
        [string]$TagName
    )

    $existingLocal = git -C $RepoRoot tag --list $TagName
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel consultar as tags locais."
    }

    return -not [string]::IsNullOrWhiteSpace(($existingLocal | Out-String))
}

function Resolve-OutputRoot {
    param(
        [string]$RepoRoot,
        [string]$ConfiguredOutputRoot
    )

    if ([string]::IsNullOrWhiteSpace($ConfiguredOutputRoot)) {
        return Join-Path $env:USERPROFILE "Documents\PDV-Client"
    }

    if ([System.IO.Path]::IsPathRooted($ConfiguredOutputRoot)) {
        return $ConfiguredOutputRoot
    }

    return Join-Path $RepoRoot $ConfiguredOutputRoot
}

function Convert-ToNumericVersion {
    param([string]$VersionTag)

    $clean = $VersionTag.Trim()
    if ($clean.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(1)
    }

    $clean = ($clean -split '[\+\-]')[0]
    $parts = $clean.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -eq 0 -or $parts.Count -gt 4) {
        throw "A versao '$VersionTag' nao esta em um formato suportado."
    }

    foreach ($part in $parts) {
        if ($part -notmatch '^\d+$') {
            throw "A versao '$VersionTag' precisa conter apenas partes numericas separadas por ponto."
        }
    }

    while ($parts.Count -lt 4) {
        $parts += "0"
    }

    return ($parts -join '.')
}

function Resolve-InteractiveParameters {
    if ([string]::IsNullOrWhiteSpace($script:Version)) {
        if (-not [string]::IsNullOrWhiteSpace($DefaultVersion)) {
            $script:Version = $DefaultVersion
        }
        else {
            $typedVersion = Read-Host "Informe a versao da release (ex: v1.1.0)"
            $script:Version = $typedVersion
        }
    }

    if ([string]::IsNullOrWhiteSpace($script:Runtime) -and -not [string]::IsNullOrWhiteSpace($DefaultRuntime)) {
        $script:Runtime = $DefaultRuntime
    }

    if ([string]::IsNullOrWhiteSpace($script:Configuration) -and -not [string]::IsNullOrWhiteSpace($DefaultConfiguration)) {
        $script:Configuration = $DefaultConfiguration
    }

    if ([string]::IsNullOrWhiteSpace($script:OutputRoot) -and -not [string]::IsNullOrWhiteSpace($DefaultOutputRoot)) {
        $script:OutputRoot = $DefaultOutputRoot
    }

    if ([string]::IsNullOrWhiteSpace($script:Version)) {
        throw "A versao da release nao foi informada."
    }
}

function New-ClientPackage {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string]$Runtime,
        [string]$Configuration,
        [string]$OutputRoot,
        [string]$PackagePrefix
    )

    $projectPath = Join-Path $RepoRoot "src\Pdv.Ui\Pdv.Ui.csproj"
    $resolvedOutputRoot = Resolve-OutputRoot -RepoRoot $RepoRoot -ConfiguredOutputRoot $OutputRoot
    $packageFolder = Join-Path $resolvedOutputRoot "PDV-Client"
    $zipPath = Join-Path $resolvedOutputRoot "$PackagePrefix-$Version.zip"

    New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null

    if (Test-Path $packageFolder) {
        Remove-Item -LiteralPath $packageFolder -Recurse -Force
    }

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null

    $numericVersion = Convert-ToNumericVersion -VersionTag $Version

    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:Version=$numericVersion `
        -p:AssemblyVersion=$numericVersion `
        -p:FileVersion=$numericVersion `
        -p:InformationalVersion=$Version `
        -o $packageFolder

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao publicar o pacote do cliente."
    }

    Get-ChildItem -Path $packageFolder -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Set-Content -Path (Join-Path $packageFolder "version.txt") -Value $Version -Encoding UTF8

    $readmePath = Join-Path $packageFolder "LEIA-ME-CLIENTE.txt"
    @(
        "PDV Desktop - $Version",
        "",
        "Este pacote representa a release publicada no GitHub.",
        "A implantacao inicial do cliente deve ser feita com o script scripts\\install-client-latest.ps1.",
        "",
        "Arquivos importantes:",
        "- Pdv.Ui.exe",
        "- bibliotecas e arquivos de suporte",
        "- appsettings.json",
        "- version.txt"
    ) | Set-Content -Path $readmePath -Encoding UTF8

    Compress-Archive -Path (Join-Path $packageFolder "*") -DestinationPath $zipPath

    return @{
        PackageFolder = $packageFolder
        ZipPath = $zipPath
        OutputRoot = $resolvedOutputRoot
    }
}

function New-GitTagAndPush {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string]$BranchName
    )

    git -C $RepoRoot tag -a $Version -m "Release $Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao criar a tag '$Version'."
    }

    git -C $RepoRoot push origin $BranchName
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao enviar a branch '$BranchName'."
    }

    git -C $RepoRoot push origin $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao enviar a tag '$Version'."
    }
}

function Get-GitHubReleaseByTag {
    param(
        [hashtable]$Repository,
        [string]$Version,
        [hashtable]$Headers
    )

    try {
        return Invoke-RestMethod `
            -Method Get `
            -Uri "https://api.github.com/repos/$($Repository.Owner)/$($Repository.Name)/releases/tags/$Version" `
            -Headers $Headers
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) {
            return $null
        }

        throw
    }
}

function Get-GitHubReleaseAssets {
    param(
        [hashtable]$Repository,
        [int64]$ReleaseId,
        [hashtable]$Headers
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri "https://api.github.com/repos/$($Repository.Owner)/$($Repository.Name)/releases/$ReleaseId/assets" `
        -Headers $Headers
}

function Remove-GitHubReleaseAsset {
    param(
        [hashtable]$Repository,
        [int64]$AssetId,
        [hashtable]$Headers
    )

    Invoke-RestMethod `
        -Method Delete `
        -Uri "https://api.github.com/repos/$($Repository.Owner)/$($Repository.Name)/releases/assets/$AssetId" `
        -Headers $Headers | Out-Null
}

function Ensure-GitHubRelease {
    param(
        [hashtable]$Repository,
        [string]$Version,
        [hashtable]$Headers
    )

    $existingRelease = Get-GitHubReleaseByTag -Repository $Repository -Version $Version -Headers $Headers
    if ($existingRelease) {
        return $existingRelease
    }

    $releaseBody = @{
        tag_name = $Version
        name = $Version
        generate_release_notes = $true
    } | ConvertTo-Json

    return Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.github.com/repos/$($Repository.Owner)/$($Repository.Name)/releases" `
        -Headers $Headers `
        -Body $releaseBody `
        -ContentType "application/json"
}

function Publish-GitHubRelease {
    param(
        [hashtable]$Repository,
        [string]$Version,
        [string]$ZipPath
    )

    $token = $env:GITHUB_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = $env:GH_TOKEN
    }

    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Warning "GITHUB_TOKEN/GH_TOKEN nao configurado. A tag foi publicada, mas o Release precisara ser criado manualmente no GitHub."
        return $null
    }

    $headers = @{
        Authorization = "Bearer $token"
        Accept = "application/vnd.github+json"
        "User-Agent" = "projeto-pdv-release-script"
    }

    $release = Ensure-GitHubRelease -Repository $Repository -Version $Version -Headers $headers

    $uploadUrl = $release.upload_url -replace '\{.*\}$', ''
    $assetName = [System.IO.Path]::GetFileName($ZipPath)
    $uploadUri = [System.UriBuilder]::new($uploadUrl)
    $uploadUri.Query = "name=$([System.Uri]::EscapeDataString($assetName))"

    $assets = Get-GitHubReleaseAssets -Repository $Repository -ReleaseId $release.id -Headers $headers
    foreach ($asset in @($assets)) {
        if ($asset.name -eq $assetName) {
            Remove-GitHubReleaseAsset -Repository $Repository -AssetId $asset.id -Headers $headers
        }
    }

    Invoke-RestMethod `
        -Method Post `
        -Uri $uploadUri.Uri.AbsoluteUri `
        -Headers @{
            Authorization = "Bearer $token"
            Accept = "application/vnd.github+json"
            "User-Agent" = "projeto-pdv-release-script"
            "Content-Type" = "application/zip"
        } `
        -InFile $ZipPath | Out-Null

    return $release.html_url
}

$repoRoot = Get-RepositoryRoot
$branchName = Get-CurrentBranch
$repository = Get-OriginRepository

Resolve-InteractiveParameters

if (-not $AllowDirty) {
    Write-Step "Validando estado do Git"
    Assert-CleanWorktree -RepoRoot $repoRoot
}

if (-not $SkipTests) {
    Write-Step "Executando testes"
    dotnet test (Join-Path $repoRoot "PdvDesktop.sln")
    if ($LASTEXITCODE -ne 0) {
        throw "Os testes falharam. Release cancelado."
    }
}

Write-Step "Gerando pacote da release"
$package = New-ClientPackage `
    -RepoRoot $repoRoot `
    -Version $Version `
    -Runtime $Runtime `
    -Configuration $Configuration `
    -OutputRoot $OutputRoot `
    -PackagePrefix $PackagePrefix

if (-not $SkipGit) {
    if (Test-GitTagExists -RepoRoot $repoRoot -TagName $Version) {
        Write-Step "Tag ja existente"
        Write-Host "A tag $Version ja existe. O script vai reutilizar essa tag e seguir para a etapa de release." -ForegroundColor Yellow
    }
    else {
        Write-Step "Criando tag e enviando para o GitHub"
        New-GitTagAndPush -RepoRoot $repoRoot -Version $Version -BranchName $branchName
    }
}

$releaseUrl = $null
if (-not $SkipRelease) {
    Write-Step "Publicando release no GitHub"
    $releaseUrl = Publish-GitHubRelease -Repository $repository -Version $Version -ZipPath $package.ZipPath
}

Write-Host ""
Write-Host "Release preparada com sucesso." -ForegroundColor Green
Write-Host "Versao:        $Version"
Write-Host "Pacote:        $($package.ZipPath)"
Write-Host "Pasta:         $($package.PackageFolder)"
Write-Host "Repositorio:   $($repository.Owner)/$($repository.Name)"
if ($releaseUrl) {
    Write-Host "Release URL:   $releaseUrl"
}
elseif (-not $SkipRelease) {
    Write-Host "Release URL:   https://github.com/$($repository.Owner)/$($repository.Name)/releases/new?tag=$Version"
}
