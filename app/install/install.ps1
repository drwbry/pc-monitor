# Install Marsh PC Monitor for the current user.
param(
  [switch]$Publish,
  [string]$Configuration = "Release",
  [switch]$FrameworkDependent,
  [switch]$RegisterStartupTask
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appName = "PcMonitor"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$scriptsDest = Join-Path ([Environment]::GetFolderPath('MyDocuments')) "SysLogs\scripts"

if ($Publish) {
  $publishArgs = @{ Configuration = $Configuration }
  if ($FrameworkDependent) { $publishArgs['FrameworkDependent'] = $true }
  & (Join-Path $PSScriptRoot "publish.ps1") @publishArgs
}

$publishDir = Join-Path $root "src/PcMonitor.App/bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/publish"
$exe = Join-Path $publishDir "PcMonitor.exe"
if (-not (Test-Path $exe)) {
  throw "PcMonitor.exe not found at $exe. Run install.ps1 -Publish first."
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Force $exe $installDir

# Copy the companion scripts so the capture buttons work.
$filesDir = Join-Path (Split-Path -Parent $root) "files"
New-Item -ItemType Directory -Force -Path $scriptsDest | Out-Null
foreach ($name in @("diagnose.ps1", "live-probe.ps1", "collect-stats.ps1")) {
  $src = Join-Path $filesDir $name
  if (Test-Path $src) { Copy-Item -Force $src $scriptsDest }
}

# Start Menu shortcut.
$startMenu = [Environment]::GetFolderPath("StartMenu")
$shortcut = Join-Path $startMenu "Programs\Marsh PC Monitor.lnk"
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($shortcut)
$sc.TargetPath = Join-Path $installDir "PcMonitor.exe"
$sc.WorkingDirectory = $installDir
$sc.IconLocation = Join-Path $installDir "PcMonitor.exe"
$sc.Save()

Write-Host "Installed:  $installDir\PcMonitor.exe"
Write-Host "Shortcut:   $shortcut"
Write-Host "Scripts:    $scriptsDest"

if ($RegisterStartupTask) {
  $taskName = "Marsh PC Monitor"
  $action   = New-ScheduledTaskAction -Execute (Join-Path $installDir "PcMonitor.exe")
  $trigger  = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
  $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Highest -LogonType Interactive
  $settings  = New-ScheduledTaskSettingsSet -ExecutionTimeLimit 0 `
                 -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
  Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
  Write-Host "Startup task: '$taskName' registered (runs elevated at login, no UAC prompt)"
}

Write-Host ""
Write-Host "Launch from Start Menu, or run: $installDir\PcMonitor.exe"
