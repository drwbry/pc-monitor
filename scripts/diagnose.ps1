# diagnose.ps1
# Full system diagnostic for Claude Code analysis
# Run manually when you want a deep-dive snapshot
# Output: C:\Users\<you>\Documents\SysLogs\diagnostic_[timestamp].txt

$OutputDir = "$env:USERPROFILE\Documents\SysLogs"
$Timestamp  = Get-Date -Format "yyyy-MM-dd_HH-mm"
$OutputFile = "$OutputDir\diagnostic_$Timestamp.txt"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Clear/create output file
"" | Out-File $OutputFile

function Write-Section {
    param([string]$Title, [string]$Content)
    $line = "=" * 70
    $block = "`n$line`n  $Title`n$line`n$Content`n"
    $block | Tee-Object -Append -FilePath $OutputFile
}

# -------------------------------------------------------------------
# 1. SYSTEM OVERVIEW
# -------------------------------------------------------------------
$os   = Get-CimInstance Win32_OperatingSystem
$sys  = Get-CimInstance Win32_ComputerSystem
$cpu  = Get-CimInstance Win32_Processor
$ramTotalGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
$ramFreeGB  = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
$ramUsedGB  = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1MB, 2)
$ramUsedPct = [math]::Round($ramUsedGB / $ramTotalGB * 100, 1)
$uptime     = (Get-Date) - $os.LastBootUpTime

$overview = @"
Host        : $($sys.Name)
OS          : $($os.Caption) (Build $($os.BuildNumber))
CPU         : $($cpu.Name)
Cores       : $($cpu.NumberOfCores) physical / $($cpu.NumberOfLogicalProcessors) logical
RAM Total   : $ramTotalGB GB
RAM Used    : $ramUsedGB GB ($ramUsedPct%)
RAM Free    : $ramFreeGB GB
Last Boot   : $($os.LastBootUpTime)
Uptime      : $($uptime.Days)d $($uptime.Hours)h $($uptime.Minutes)m
"@
Write-Section "SYSTEM OVERVIEW" $overview

# -------------------------------------------------------------------
# 1a. POWER PLAN & BATTERY (laptop slowdown #1 cause)
# -------------------------------------------------------------------
$activeScheme = (powercfg /getactivescheme) -join " "
$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue

$batteryStatusMap = @{
    1='Discharging (on battery)'; 2='AC power'; 3='Fully charged';
    4='Low'; 5='Critical'; 6='Charging'; 7='Charging + High';
    8='Charging + Low'; 9='Charging + Critical'; 10='Undefined'; 11='Partially charged'
}

if ($battery) {
    $statusText = $batteryStatusMap[[int]$battery.BatteryStatus]
    if (-not $statusText) { $statusText = "Status code $($battery.BatteryStatus)" }

    $wearText = "(design/full-charge capacity not reported by this battery)"
    if ($battery.DesignCapacity -and $battery.FullChargeCapacity) {
        $wearPct = [math]::Round((1 - ($battery.FullChargeCapacity / $battery.DesignCapacity)) * 100, 1)
        $wearText = "Design: $($battery.DesignCapacity) mWh | Full charge: $($battery.FullChargeCapacity) mWh | Wear: $wearPct%"
    }

    $powerBlock = @"
Active scheme : $activeScheme
Battery       : $statusText
Charge %      : $($battery.EstimatedChargeRemaining)
Battery wear  : $wearText
"@
} else {
    $powerBlock = @"
Active scheme : $activeScheme
Battery       : (no battery detected - desktop or AC-only system)
"@
}
Write-Section "POWER PLAN & BATTERY" $powerBlock

# -------------------------------------------------------------------
# 1b. REAL-TIME CPU SAMPLE (per-process, not cumulative)
# -------------------------------------------------------------------
try {
    $cpuSample = Get-Counter '\Process(*)\% Processor Time' -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop
    $logicalCores = (Get-CimInstance Win32_Processor | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
    if (-not $logicalCores) { $logicalCores = 1 }

    $latest = $cpuSample[-1].CounterSamples |
        Where-Object { $_.InstanceName -and $_.InstanceName -notin @('_total','idle') } |
        Sort-Object CookedValue -Descending |
        Select-Object -First 15 |
        ForEach-Object {
            [PSCustomObject]@{
                Process = $_.InstanceName
                'CPU %' = [math]::Round($_.CookedValue / $logicalCores, 1)
            }
        }

    $totalCounter = $cpuSample[-1].CounterSamples | Where-Object { $_.InstanceName -eq '_total' }
    $totalPct = if ($totalCounter) { [math]::Round($totalCounter.CookedValue / $logicalCores, 1) } else { 'n/a' }

    $cpuRealtime = "System-wide CPU at sample: $totalPct%`n`n" + ($latest | Format-Table -AutoSize | Out-String)
} catch {
    $cpuRealtime = "(Could not collect real-time CPU counters: $($_.Exception.Message))"
}
Write-Section "REAL-TIME CPU SAMPLE (top 15, normalized to logical cores)" $cpuRealtime

# -------------------------------------------------------------------
# 1c. GPU STATE (NVIDIA via nvidia-smi if present, otherwise WMI)
# -------------------------------------------------------------------
$nvidiaSmi = "$env:ProgramFiles\NVIDIA Corporation\NVSMI\nvidia-smi.exe"
if (-not (Test-Path $nvidiaSmi)) {
    $cmd = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
    if ($cmd) { $nvidiaSmi = $cmd.Source } else { $nvidiaSmi = $null }
}

$gpuBlock = ""
if ($nvidiaSmi) {
    $gpuQuery = & $nvidiaSmi --query-gpu=name,temperature.gpu,utilization.gpu,utilization.memory,memory.used,memory.total,power.draw,clocks.current.graphics,clocks.max.graphics --format=csv 2>&1 | Out-String
    $gpuApps  = & $nvidiaSmi --query-compute-apps=pid,process_name,used_memory --format=csv 2>&1 | Out-String
    $gpuBlock = "nvidia-smi: $nvidiaSmi`n`n--- GPU state ---`n$gpuQuery`n--- GPU compute apps ---`n$gpuApps"
} else {
    $vcs = Get-CimInstance Win32_VideoController |
        Select-Object Name, DriverVersion,
            @{N='VRAM (GB)'; E={ if ($_.AdapterRAM) { [math]::Round($_.AdapterRAM/1GB,2) } else { 'n/a' } }},
            CurrentRefreshRate, VideoModeDescription |
        Format-Table -AutoSize -Wrap | Out-String
    $gpuBlock = "(nvidia-smi not found - falling back to Win32_VideoController)`n$vcs"
}
Write-Section "GPU STATE" $gpuBlock

# -------------------------------------------------------------------
# 1d. THERMAL HINTS (ACPI thermal zones - only some laptops expose these)
# -------------------------------------------------------------------
try {
    $thermal = Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction Stop
    if ($thermal) {
        $thermalTable = $thermal | ForEach-Object {
            $celsius = [math]::Round(($_.CurrentTemperature / 10) - 273.15, 1)
            [PSCustomObject]@{
                Zone    = $_.InstanceName
                'Temp C' = $celsius
            }
        } | Format-Table -AutoSize | Out-String
        $thermalBlock = $thermalTable
    } else {
        $thermalBlock = "(No thermal zones reported)"
    }
} catch {
    $thermalBlock = "(MSAcpi_ThermalZoneTemperature unavailable - many laptops do not expose this. Use vendor tooling like HWiNFO64 for accurate temps.)"
}
Write-Section "THERMAL ZONES (>=85C suggests throttling)" $thermalBlock

# -------------------------------------------------------------------
# 1e. WINDOWS DEFENDER STATE (active scans tank performance)
# -------------------------------------------------------------------
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    $mpBlock = @"
Real-time protection : $($mp.RealTimeProtectionEnabled)
Tamper protection    : $($mp.IsTamperProtected)
Last quick scan      : $($mp.QuickScanStartTime) -> $($mp.QuickScanEndTime)
Last full scan       : $($mp.FullScanStartTime)  -> $($mp.FullScanEndTime)
Signatures updated   : $($mp.AntivirusSignatureLastUpdated)
Engine version       : $($mp.AMEngineVersion)
"@

    $defenderProcs = Get-Process MsMpEng, NisSrv -ErrorAction SilentlyContinue |
        Select-Object Name, Id,
            @{N='CPU (s)'; E={[math]::Round($_.CPU,1)}},
            @{N='RAM (MB)'; E={[math]::Round($_.WorkingSet/1MB,1)}} |
        Format-Table -AutoSize | Out-String
    if ($defenderProcs.Trim()) { $mpBlock += "`n--- Defender process state ---`n$defenderProcs" }
} catch {
    $mpBlock = "(Get-MpComputerStatus failed: $($_.Exception.Message))"
}
Write-Section "WINDOWS DEFENDER STATE" $mpBlock

# -------------------------------------------------------------------
# 1f. MEMORY PRESSURE & PAGING
# -------------------------------------------------------------------
try {
    $memCounter = Get-Counter '\Memory\Available MBytes','\Memory\% Committed Bytes In Use','\Memory\Pages/sec' -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop
    $memSamples = $memCounter[-1].CounterSamples |
        Select-Object Path, @{N='Value'; E={[math]::Round($_.CookedValue,2)}} |
        Format-Table -AutoSize | Out-String
    $memBlock = "$memSamples`nHigh % Committed + non-trivial Pages/sec => system is paging."
} catch {
    $memBlock = "(Memory counters unavailable: $($_.Exception.Message))"
}
Write-Section "MEMORY PRESSURE & PAGING" $memBlock

# -------------------------------------------------------------------
# 1g. DISK I/O SNAPSHOT
# -------------------------------------------------------------------
try {
    $diskCounter = Get-Counter '\PhysicalDisk(*)\% Idle Time','\PhysicalDisk(*)\Avg. Disk Queue Length' -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop
    $diskSamples = $diskCounter[-1].CounterSamples |
        Where-Object { $_.InstanceName -ne '_total' } |
        Select-Object @{N='Disk'; E={$_.InstanceName}},
                      @{N='Counter'; E={ ($_.Path -split '\\')[-1] }},
                      @{N='Value'; E={[math]::Round($_.CookedValue,2)}} |
        Sort-Object Disk, Counter |
        Format-Table -AutoSize | Out-String
    $diskBlock = "$diskSamples`nLow % Idle Time + high Avg Disk Queue Length => disk-bound."
} catch {
    $diskBlock = "(PhysicalDisk counters unavailable: $($_.Exception.Message))"
}
Write-Section "DISK I/O SNAPSHOT" $diskBlock

# -------------------------------------------------------------------
# 1h. WINDOWS UPDATE / SERVICING ACTIVITY
# -------------------------------------------------------------------
$wuProcs = Get-Process TiWorker, TrustedInstaller, MoUsoCoreWorker, UsoClient, wuauclt -ErrorAction SilentlyContinue |
    Select-Object Name, Id,
        @{N='CPU (s)'; E={[math]::Round($_.CPU,1)}},
        @{N='RAM (MB)'; E={[math]::Round($_.WorkingSet/1MB,1)}} |
    Format-Table -AutoSize | Out-String

$wuauserv = Get-Service wuauserv, UsoSvc, BITS -ErrorAction SilentlyContinue |
    Select-Object Name, Status, StartType |
    Format-Table -AutoSize | Out-String

$wuBlock = if ($wuProcs.Trim()) {
    "Running update/servicing processes:`n$wuProcs`nUpdate services:`n$wuauserv"
} else {
    "(No active TiWorker/TrustedInstaller/UsoClient processes detected.)`n`nUpdate services:`n$wuauserv"
}
Write-Section "WINDOWS UPDATE / SERVICING ACTIVITY" $wuBlock

# -------------------------------------------------------------------
# 2. DISK USAGE
# -------------------------------------------------------------------
$diskInfo = Get-PSDrive -PSProvider FileSystem |
    Where-Object { $_.Used -gt 0 } |
    Select-Object Name,
        @{N='Used GB' ; E={[math]::Round($_.Used  / 1GB, 2)}},
        @{N='Free GB' ; E={[math]::Round($_.Free  / 1GB, 2)}},
        @{N='Total GB'; E={[math]::Round(($_.Used + $_.Free) / 1GB, 2)}},
        @{N='Used %'  ; E={[math]::Round($_.Used  / ($_.Used + $_.Free) * 100, 1)}} |
    Format-Table -AutoSize | Out-String
Write-Section "DISK USAGE" $diskInfo

# -------------------------------------------------------------------
# 3. TOP PROCESSES - CPU
# -------------------------------------------------------------------
$topCPU = Get-Process |
    Sort-Object CPU -Descending |
    Select-Object -First 25 |
    Format-Table Name,
        @{N='CPU (s)' ; E={[math]::Round($_.CPU, 1)}},
        @{N='RAM (MB)'; E={[math]::Round($_.WorkingSet / 1MB, 1)}},
        Id -AutoSize |
    Out-String
Write-Section "TOP 25 PROCESSES BY CPU" $topCPU

# -------------------------------------------------------------------
# 4. TOP PROCESSES - RAM
# -------------------------------------------------------------------
$topRAM = Get-Process |
    Sort-Object WorkingSet -Descending |
    Select-Object -First 25 |
    Format-Table Name,
        @{N='RAM (MB)'; E={[math]::Round($_.WorkingSet / 1MB, 1)}},
        @{N='CPU (s)' ; E={[math]::Round($_.CPU, 1)}},
        Id -AutoSize |
    Out-String
Write-Section "TOP 25 PROCESSES BY RAM" $topRAM

# -------------------------------------------------------------------
# 5. STARTUP PROGRAMS
# -------------------------------------------------------------------
$startupHKLM = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue
$startupHKCU = Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue

$startupList = @()
foreach ($prop in ($startupHKLM | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -notlike 'PS*' })) {
    $startupList += [PSCustomObject]@{ Name=$prop.Name; Hive='HKLM'; Command=$startupHKLM.$($prop.Name) }
}
foreach ($prop in ($startupHKCU | Get-Member -MemberType NoteProperty | Where-Object { $_.Name -notlike 'PS*' })) {
    $startupList += [PSCustomObject]@{ Name=$prop.Name; Hive='HKCU'; Command=$startupHKCU.$($prop.Name) }
}
$startupTable = $startupList | Format-Table Name, Hive, Command -AutoSize | Out-String
Write-Section "STARTUP PROGRAMS (REGISTRY)" $startupTable

# -------------------------------------------------------------------
# 6. RUNNING THIRD-PARTY SERVICES
# -------------------------------------------------------------------
$thirdPartyServices = Get-CimInstance Win32_Service |
    Where-Object {
        $_.State -eq 'Running' -and
        $_.PathName -notlike '*\Windows\*' -and
        $_.PathName -notlike '*system32*' -and
        $_.PathName -notlike '*SysWOW64*'
    } |
    Select-Object Name, DisplayName,
        @{N='Path (truncated)'; E={ if ($_.PathName.Length -gt 80) { $_.PathName.Substring(0,80)+'...' } else { $_.PathName } }} |
    Sort-Object Name |
    Format-Table -AutoSize -Wrap | Out-String
Write-Section "RUNNING THIRD-PARTY SERVICES" $thirdPartyServices

# -------------------------------------------------------------------
# 7. SCHEDULED TASKS (NON-MICROSOFT)
# -------------------------------------------------------------------
$tasks = Get-ScheduledTask |
    Where-Object { $_.TaskPath -notlike '\Microsoft\*' -and $_.State -ne 'Disabled' } |
    Select-Object TaskName, State, TaskPath |
    Sort-Object TaskName |
    Format-Table -AutoSize | Out-String
Write-Section "SCHEDULED TASKS (NON-MICROSOFT, NON-DISABLED)" $tasks

# -------------------------------------------------------------------
# 8. PAGE FILE
# -------------------------------------------------------------------
$pageFile = Get-CimInstance Win32_PageFileUsage |
    Select-Object Name,
        @{N='Allocated (MB)'; E={$_.AllocatedBaseSize}},
        @{N='Current (MB)'  ; E={$_.CurrentUsage}},
        @{N='Peak (MB)'     ; E={$_.PeakUsage}} |
    Format-Table -AutoSize | Out-String
Write-Section "PAGE FILE USAGE" $pageFile

# -------------------------------------------------------------------
# 9. TEMP FOLDER SIZES
# -------------------------------------------------------------------
$userTempMB = [math]::Round(
    (Get-ChildItem $env:TEMP -Recurse -ErrorAction SilentlyContinue |
     Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$winTempMB = [math]::Round(
    (Get-ChildItem "C:\Windows\Temp" -Recurse -ErrorAction SilentlyContinue |
     Measure-Object -Property Length -Sum).Sum / 1MB, 1)

$tempInfo = "User Temp (%TEMP%) : $userTempMB MB`nWindows Temp        : $winTempMB MB"
Write-Section "TEMP FOLDER SIZES" $tempInfo

# -------------------------------------------------------------------
# 10. RECENT SYSTEM ERRORS (last 24h)
# -------------------------------------------------------------------
try {
    $sysErrors = Get-EventLog -LogName System -EntryType Error -After (Get-Date).AddHours(-24) -Newest 30 -ErrorAction Stop |
        Select-Object TimeGenerated, Source,
            @{N='Message'; E={ $_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)) }} |
        Format-Table -AutoSize -Wrap | Out-String
} catch { $sysErrors = "(No system errors in last 24h or log unavailable)" }
Write-Section "SYSTEM ERRORS (LAST 24H)" $sysErrors

# -------------------------------------------------------------------
# 11. RECENT APPLICATION ERRORS (last 24h)
# -------------------------------------------------------------------
try {
    $appErrors = Get-EventLog -LogName Application -EntryType Error -After (Get-Date).AddHours(-24) -Newest 30 -ErrorAction Stop |
        Select-Object TimeGenerated, Source,
            @{N='Message'; E={ $_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)) }} |
        Format-Table -AutoSize -Wrap | Out-String
} catch { $appErrors = "(No app errors in last 24h or log unavailable)" }
Write-Section "APPLICATION ERRORS (LAST 24H)" $appErrors

# -------------------------------------------------------------------
# FOOTER
# -------------------------------------------------------------------
$footer = @"
Diagnostic saved to : $OutputFile
WSL path            : /mnt/c/Users/$env:USERNAME/Documents/SysLogs/diagnostic_$Timestamp.txt

To analyze in Claude Code (WSL):
  cat '/mnt/c/Users/$env:USERNAME/Documents/SysLogs/diagnostic_$Timestamp.txt'
"@
Write-Host "`n$footer" -ForegroundColor Cyan
