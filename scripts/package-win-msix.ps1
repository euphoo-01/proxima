param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0.0",
    [string]$Publisher = "CN=ProximaDev"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$PublishScript = Join-Path $RootDir "scripts/publish-win-x64.ps1"
$PublishDir = Join-Path $RootDir "artifacts/publish/win-x64"
$MsixRoot = Join-Path $RootDir "artifacts/packages/msix"
$LayoutDir = Join-Path $MsixRoot "layout"
$OutMsix = Join-Path $MsixRoot ("Proxima_" + $Version + "_x64.msix")
$ManifestPath = Join-Path $LayoutDir "AppxManifest.xml"

& $PublishScript -Configuration $Configuration

if (!(Get-Command makeappx.exe -ErrorAction SilentlyContinue)) {
    throw "makeappx.exe not found. Install Windows SDK and retry."
}

if (Test-Path $LayoutDir) {
    Remove-Item -Recurse -Force $LayoutDir
}
New-Item -ItemType Directory -Path $LayoutDir | Out-Null
New-Item -ItemType Directory -Path $MsixRoot -Force | Out-Null

Copy-Item -Path (Join-Path $PublishDir "*") -Destination $LayoutDir -Recurse

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         IgnorableNamespaces="uap">
  <Identity Name="Proxima.App" Publisher="$Publisher" Version="$Version" />
  <Properties>
    <DisplayName>Proxima</DisplayName>
    <PublisherDisplayName>Proxima Team</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="App" Executable="Proxima.App.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Proxima" Description="Local-first financial analytics" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" BackgroundColor="transparent" />
    </Application>
  </Applications>
</Package>
"@

New-Item -ItemType Directory -Path (Join-Path $LayoutDir "Assets") -Force | Out-Null
[IO.File]::WriteAllText($ManifestPath, $manifest)

if (!(Test-Path (Join-Path $LayoutDir "Assets\StoreLogo.png"))) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 150,150
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255,35,113,228))
    $bmp.Save((Join-Path $LayoutDir "Assets\StoreLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $g.Dispose()

    Copy-Item (Join-Path $LayoutDir "Assets\StoreLogo.png") (Join-Path $LayoutDir "Assets\Square150x150Logo.png")
    Copy-Item (Join-Path $LayoutDir "Assets\StoreLogo.png") (Join-Path $LayoutDir "Assets\Square44x44Logo.png")
}

if (Test-Path $OutMsix) {
    Remove-Item -Force $OutMsix
}

makeappx.exe pack /d $LayoutDir /p $OutMsix /o | Out-Null
Write-Host "Built MSIX package: $OutMsix"
Write-Host "Note: package signing is not included in this script."
