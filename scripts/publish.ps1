param(
    [ValidateSet('all', 'standalone', 'framework-dependent')]
    [string] $Mode = 'all',

    [string] $Version = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SheraBoard.App\SheraBoard.App.csproj'
$artifactsDir = Join-Path $root 'artifacts'
$releaseDir = Join-Path $artifactsDir 'release'
$stagingRoot = Join-Path $artifactsDir '.staging'
$runtime = 'win-x64'

function Get-ReleaseVersion {
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        return $Version.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        return $env:GITHUB_REF_NAME.Trim()
    }

    return "dev-$(Get-Date -Format 'yyyyMMddHHmmss')"
}

function Assert-ChildPath([string] $Path) {
    $rootFull = [System.IO.Path]::GetFullPath($root).TrimEnd('\')
    $targetFull = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = $rootFull + '\'

    if ($targetFull -ne $rootFull -and -not $targetFull.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside repository: $targetFull"
    }

    return $targetFull
}

function Reset-Directory([string] $Path) {
    $fullPath = Assert-ChildPath $Path
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
    return $fullPath
}

function Invoke-DotnetPublish(
    [string] $PackageKind,
    [bool] $SelfContained,
    [bool] $EnableCompression
) {
    $stagingDir = Reset-Directory (Join-Path $stagingRoot "$runtime-$PackageKind")
    $selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
    $publishArgs = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $runtime,
        '--self-contained', $selfContainedValue,
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        '-o', $stagingDir
    )

    if ($EnableCompression) {
        $publishArgs += '-p:EnableCompressionInSingleFile=true'
    }

    & dotnet @publishArgs | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $exe = Join-Path $stagingDir 'SheraBoard.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Publish did not create SheraBoard.exe in $stagingDir"
    }

    return $stagingDir
}

function New-ReleaseZip([string] $PackageKind, [string] $StagingDir, [string] $ReleaseVersion) {
    $zipName = "SheraBoard-$ReleaseVersion-$runtime-$PackageKind.zip"
    $zipPath = Join-Path $releaseDir $zipName

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $StagingDir '*') -DestinationPath $zipPath -Force
    return $zipPath
}

$releaseVersion = Get-ReleaseVersion
$releaseDir = Reset-Directory $releaseDir
$stagingRoot = Reset-Directory $stagingRoot
$packages = New-Object System.Collections.Generic.List[string]

if ($Mode -eq 'all' -or $Mode -eq 'standalone') {
    $standaloneStaging = Invoke-DotnetPublish `
        -PackageKind 'standalone' `
        -SelfContained $true `
        -EnableCompression $true
    $packages.Add((New-ReleaseZip -PackageKind 'standalone' -StagingDir $standaloneStaging -ReleaseVersion $releaseVersion)) | Out-Null
}

if ($Mode -eq 'all' -or $Mode -eq 'framework-dependent') {
    $frameworkStaging = Invoke-DotnetPublish `
        -PackageKind 'framework-dependent' `
        -SelfContained $false `
        -EnableCompression $false
    $packages.Add((New-ReleaseZip -PackageKind 'framework-dependent' -StagingDir $frameworkStaging -ReleaseVersion $releaseVersion)) | Out-Null
}

$hashFile = Join-Path $releaseDir 'SHA256SUMS.txt'
$packages |
    ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 } |
    ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_.Path))" } |
    Set-Content -LiteralPath $hashFile -Encoding ascii

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Write-Host "Release packages created in:"
Write-Host $releaseDir
Get-ChildItem -LiteralPath $releaseDir -File | Select-Object Name, Length
