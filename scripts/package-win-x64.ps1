param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$PublishScript = Join-Path $RootDir "scripts/publish-win-x64.ps1"
$PublishDir = Join-Path $RootDir "artifacts/publish/win-x64"
$PackageDir = Join-Path $RootDir "artifacts/packages"
$PackageName = "proxima-win-x64-v$Version.zip"
$PackagePath = Join-Path $PackageDir $PackageName

& $PublishScript -Configuration $Configuration

if (!(Test-Path $PackageDir)) {
    New-Item -ItemType Directory -Path $PackageDir | Out-Null
}

if (Test-Path $PackagePath) {
    Remove-Item -Force $PackagePath
}

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $PackagePath
Write-Host "Packaged Windows artifact: $PackagePath"
