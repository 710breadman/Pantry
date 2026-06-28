[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipSmokeTest,
    [ValidatePattern('^\d+\.\d+\.\d+([-.+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',
    [string]$SigningCertificateThumbprint,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$solution = Join-Path $repoRoot 'src\DevToolsCurator.slnx'
$appProject = Join-Path $repoRoot 'src\DevToolsCurator.App\DevToolsCurator.App.csproj'
$testsProject = Join-Path $repoRoot 'src\DevToolsCurator.Tests\DevToolsCurator.Tests.csproj'
$releaseParent = Join-Path $repoRoot 'release'
$releaseRoot = Join-Path $repoRoot 'release\DevKit'
$publishDir = Join-Path $releaseParent '.publish-temp-DevKit'
$exePath = Join-Path $releaseRoot 'DevKit.exe'

function Resolve-DotNet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $defaultPath = 'C:\Program Files\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $defaultPath) {
        return $defaultPath
    }

    throw 'The .NET SDK was not found. Install the .NET SDK, then rerun build-release.ps1.'
}

function Assert-UnderRepo {
    param([Parameter(Mandatory)] [string]$Path)
    $resolvedRoot = [System.IO.Path]::GetFullPath($repoRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside repo: $resolvedPath"
    }
}

$dotnet = Resolve-DotNet
Assert-UnderRepo -Path $releaseRoot

Get-Process -Name DevKit -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([System.IO.Path]::GetFullPath($_.Path)).Equals([System.IO.Path]::GetFullPath($exePath), [System.StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force

Write-Host "Using .NET SDK: $dotnet"
& $dotnet --info | Out-Host

Write-Host 'Restoring solution...'
& $dotnet restore $solution

Write-Host 'Building Release...'
& $dotnet build $solution -c Release --no-restore /p:Version=$Version

if (-not $SkipTests) {
    Write-Host 'Running regression tests...'
    & $dotnet run --project $testsProject -c Release --no-build
}

if (Test-Path -LiteralPath $releaseRoot) {
    Assert-UnderRepo -Path $releaseRoot
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

if (Test-Path -LiteralPath $publishDir) {
    Assert-UnderRepo -Path $publishDir
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host 'Publishing self-contained win-x64 single-file EXE...'
& $dotnet publish $appProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:Version=$Version

$publishedExe = Join-Path $publishDir 'DevKit.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Publish did not create $publishedExe"
}

Move-Item -LiteralPath $publishedExe -Destination $exePath -Force
Remove-Item -LiteralPath $publishDir -Recurse -Force

$catalogPath = Join-Path $repoRoot 'tool_catalog.json'
if (Test-Path -LiteralPath $catalogPath) {
    Copy-Item -LiteralPath $catalogPath -Destination (Join-Path $releaseRoot 'tool_catalog.json') -Force
}

$configPath = Join-Path $repoRoot 'config.json'
if (Test-Path -LiteralPath $configPath) {
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $releaseRoot 'config.default.json') -Force
}

@"
DevKit
======

Version: $Version

Double-click DevKit.exe to launch the Windows developer environment curator.

Runtime behavior:
- By default, DevKit stores config in %AppData%\DevKit\config.json.
- Reports are written to %AppData%\DevKit\reports.
- Cache files are written to %LocalAppData%\DevKit\cache.
- Portable mode is enabled only when config.json, reports, cache, .portable, or DevKit.portable exists next to DevKit.exe.
- tool_catalog.json next to DevKit.exe can override the embedded catalog. If it is missing or invalid, DevKit uses the embedded default catalog and still opens.

No command prompt is required for normal use.
"@ | Set-Content -LiteralPath (Join-Path $releaseRoot 'README_RUN.txt') -Encoding UTF8

$signed = $false
if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    $normalizedThumbprint = $SigningCertificateThumbprint.Replace(' ', '')
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My |
        Where-Object { $_.Thumbprint -eq $normalizedThumbprint -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $certificate) {
        throw "Signing certificate with thumbprint $normalizedThumbprint and a private key was not found."
    }

    Write-Host "Signing DevKit.exe with certificate $normalizedThumbprint..."
    $signature = Set-AuthenticodeSignature `
        -FilePath $exePath `
        -Certificate $certificate `
        -HashAlgorithm SHA256 `
        -TimestampServer $TimestampServer
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode signing failed: $($signature.StatusMessage)"
    }
    $signed = $true
}

$sourceCommit = 'unversioned'
$sourceDirty = $null
if (Test-Path -LiteralPath (Join-Path $repoRoot '.git')) {
    $commitCount = [int](& git -C $repoRoot rev-list --count --all)
    if ($commitCount -gt 0) {
        $sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
    } else {
        $sourceCommit = 'uncommitted'
    }
    $sourceDirty = @(& git -C $repoRoot status --porcelain).Count -gt 0
}

$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
$sdkVersion = (& $dotnet --version).Trim()
$metadata = [ordered]@{
    product = 'DevKit'
    version = $Version
    built_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    source_commit = $sourceCommit
    source_dirty = $sourceDirty
    dotnet_sdk = $sdkVersion
    runtime_identifier = 'win-x64'
    self_contained = $true
    signed = $signed
    sha256 = $exeHash
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'build-metadata.json') -Encoding UTF8
"$exeHash *DevKit.exe" | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS') -Encoding ascii

if (-not $SkipSmokeTest) {
    Write-Host 'Running published EXE smoke test...'
    & $exePath --smoke-test
    if ($LASTEXITCODE -ne 0) {
        throw "Published smoke test failed with exit code $LASTEXITCODE"
    }

    Write-Host 'Running published EXE contract self-check...'
    & $exePath --contract-self-check
    if ($LASTEXITCODE -ne 0) {
        throw "Published contract self-check failed with exit code $LASTEXITCODE"
    }
}

Write-Host ''
Write-Host "Release ready: $exePath"
