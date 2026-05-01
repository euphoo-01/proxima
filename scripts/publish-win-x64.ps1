param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $RootDir "artifacts/publish/win-x64"
$Project = Join-Path $RootDir "src/Proxima.App/Proxima.App.csproj"

dotnet publish $Project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $OutDir

Write-Host "Published Windows build to: $OutDir"
