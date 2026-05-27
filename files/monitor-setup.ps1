# monitor-setup.ps1
# Run ONCE as Administrator to install the scheduled collection task
# Prerequisites: PowerShell execution policy must allow local scripts
#   Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

$ScriptDir    = "$env:USERPROFILE\Documents\SysLogs\scripts"
$CollectDest  = "$ScriptDir\collect-stats.ps1"
$CollectSrc   = Join-Path $PSScriptRoot "collect-stats.ps1"

# Create scripts directory
if (-not (Test-Path $ScriptDir)) {
    New-Item -ItemType Directory -Path $ScriptDir | Out-Null
    Write-Host "Created scripts directory: $ScriptDir" -ForegroundColor Green
}

# Copy collector script
if (Test-Path $CollectSrc) {
    Copy-Item $CollectSrc $CollectDest -Force
    Write-Host "Copied collect-stats.ps1 to $CollectDest" -ForegroundColor Green
} else {
    Write-Host "ERROR: collect-stats.ps1 not found next to this script. Place both files in the same folder." -ForegroundColor Red
    exit 1
}

# Build scheduled task
$action = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$CollectDest`""

# Repeat every hour, starting now, running indefinitely
$trigger = New-ScheduledTaskTrigger `
    -RepetitionInterval (New-TimeSpan -Hours 1) `
    -Once `
    -At (Get-Date)

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable:$false

$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType Interactive `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName    "SysMonitor-HourlyCollect" `
    -Action      $action `
    -Trigger     $trigger `
    -Settings    $settings `
    -Principal   $principal `
    -Description "Hourly system stats collection for Claude diagnostics. Managed via SysLogs\scripts." `
    -Force | Out-Null

Write-Host "`nScheduled task 'SysMonitor-HourlyCollect' installed successfully." -ForegroundColor Green
Write-Host "Stats will write to: $env:USERPROFILE\Documents\SysLogs\hourly\" -ForegroundColor Cyan
Write-Host "WSL path: /mnt/c/Users/$env:USERNAME/Documents/SysLogs/" -ForegroundColor Cyan

# Run it immediately to verify
Write-Host "`nRunning first collection now..." -ForegroundColor Yellow
Start-ScheduledTask -TaskName "SysMonitor-HourlyCollect"
Start-Sleep -Seconds 5

$latest = Get-ChildItem "$env:USERPROFILE\Documents\SysLogs\hourly" -Filter "*.json" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latest) {
    Write-Host "First collection successful: $($latest.Name)" -ForegroundColor Green
} else {
    Write-Host "First collection may still be running. Check the hourly folder in a moment." -ForegroundColor Yellow
}
