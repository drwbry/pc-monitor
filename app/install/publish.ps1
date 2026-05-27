# Build a self-contained single-file PcMonitor.exe.
param(
  [string]$Configuration = "Release",
  [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/PcMonitor.App/PcMonitor.App.csproj"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $proj -c $Configuration -r win-x64 `
  --self-contained $selfContained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

$outDir = Join-Path $root "src/PcMonitor.App/bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/publish"
Write-Host "Published to: $outDir"
