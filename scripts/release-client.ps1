param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish",
    [string]$PackagePrefix = "PDV-Cliente",
    [switch]$SkipTests,
    [switch]$SkipGit,
    [switch]$SkipRelease,
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"

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
            Url = $originUrl
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

function Remove-ExistingTag {
    param(
        [string]$RepoRoot,
        [string]$TagName
    )

    $existingLocal = git -C $RepoRoot tag --list $TagName
    if (-not [string]::IsNullOrWhiteSpace(($existingLocal | Out-String))) {
        throw "A tag '$TagName' ja existe localmente."
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

function Get-OrCreateClientPackage {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string]$Runtime,
        [string]$Configuration,
        [string]$OutputRoot,
        [string]$PackagePrefix
    )

    $projectPath = Join-Path $RepoRoot "src\Pdv.Ui\Pdv.Ui.csproj"
    $packageFolder = Join-Path $RepoRoot (Join-Path $OutputRoot $Version)
    $zipPath = Join-Path $RepoRoot (Join-Path $OutputRoot "$PackagePrefix-$Version.zip")

    if (Test-Path $zipPath) {
        Write-Host "Pacote existente encontrado para a versao $Version. Reaproveitando o arquivo atual." -ForegroundColor Yellow
        return @{
            PackageFolder = $packageFolder
            ZipPath = $zipPath
            ReusedExisting = $true
        }
    }

    if (Test-Path $packageFolder) {
        Remove-Item -LiteralPath $packageFolder -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null

    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $packageFolder

    Get-ChildItem -Path $packageFolder -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $readmePath = Join-Path $packageFolder "LEIA-ME-CLIENTE.txt"
    @(
        "PDV Desktop - $Version"
        ""
        "Arquivos:"
        "- Pdv.Ui.exe"
        "- appsettings.json"
        ""
        "Uso:"
        "1. Extraia os arquivos em uma pasta local."
        "2. Execute Pdv.Ui.exe."
        "3. As pastas 'data' e 'logs' serao criadas automaticamente."
        ""
        "Observacoes:"
        "- Mantenha o appsettings.json na mesma pasta do executavel."
        "- O banco local sera criado em .\data\pdv-local.db."
        "- Os logs de erro serao gravados em .\logs\errors-AAAAmmdd.txt."
    ) | Set-Content -Path $readmePath -Encoding UTF8

    Compress-Archive -Path (Join-Path $packageFolder "*") -DestinationPath $zipPath

    return @{
        PackageFolder = $packageFolder
        ZipPath = $zipPath
        ReusedExisting = $false
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

    try {
        return Invoke-RestMethod `
            -Method Post `
            -Uri "https://api.github.com/repos/$($Repository.Owner)/$($Repository.Name)/releases" `
            -Headers $Headers `
            -Body $releaseBody `
            -ContentType "application/json"
    }
    catch {
        $responseBody = $null
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $responseBody = $reader.ReadToEnd()
            }
            catch {
                $responseBody = $null
            }
        }

        if ($responseBody -and $responseBody -match 'already_exists') {
            return Get-GitHubReleaseByTag -Repository $Repository -Version $Version -Headers $Headers
        }

        throw
    }
}

function New-GitHubRelease {
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
        -InFile $ZipPath

    return $release.html_url
}

$repoRoot = Get-RepositoryRoot
$branchName = Get-CurrentBranch
$repository = Get-OriginRepository

if (-not $AllowDirty) {
    Write-Step "Validando estado do Git"
    Assert-CleanWorktree -RepoRoot $repoRoot
}

    if (-not $SkipGit -and -not (Test-GitTagExists -RepoRoot $repoRoot -TagName $Version)) {
        Remove-ExistingTag -RepoRoot $repoRoot -TagName $Version
    }

if (-not $SkipTests) {
    Write-Step "Executando testes"
    dotnet test (Join-Path $repoRoot "PdvDesktop.sln")
    if ($LASTEXITCODE -ne 0) {
        throw "Os testes falharam. Release cancelado."
    }
}

Write-Step "Gerando pacote do cliente"
$package = Get-OrCreateClientPackage `
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
    Write-Step "Criando Release no GitHub"
    $releaseUrl = New-GitHubRelease -Repository $repository -Version $Version -ZipPath $package.ZipPath
}

Write-Host ""
Write-Host "Release preparado com sucesso." -ForegroundColor Green
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
