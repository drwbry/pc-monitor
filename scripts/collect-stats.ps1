# collect-stats.ps1
# Lightweight hourly snapshot - runs via scheduled task
# Output: C:\Users\<you>\Documents\SysLogs\hourly\stats_[timestamp].json
# Files older than 30 days are auto-pruned.
#
# Schema v2: adds commit charge (the metric that actually predicts
# "feels sluggish"), page file usage, CPU scheduling queue length,
# and total process count.
#
# Schema v3: adds cpu_perf (% Processor Performance avg/max over ~5s + reported
# frequency) - a driver-free signal that confirms CPU clamping below base clock
# (e.g. BD PROCHOT), which load/RAM metrics cannot see.

$OutputDir = "$env:USERPROFILE\Documents\SysLogs\hourly"
$Timestamp  = Get-Date -Format "yyyy-MM-dd_HH-mm"
$OutputFile = "$OutputDir\stats_$Timestamp.json"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$os      = Get-CimInstance Win32_OperatingSystem
$cpuLoad = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
$ramTotal = $os.TotalVisibleMemorySize
$ramFree  = $os.FreePhysicalMemory

# Commit charge: total virtual memory promised to all processes.
# TotalVirtualMemorySize = commit limit (RAM + page file).
# FreeVirtualMemory      = commit limit minus commit charge.
$commitLimit = $os.TotalVirtualMemorySize
$commitFree  = $os.FreeVirtualMemory
$commitUsed  = $commitLimit - $commitFree

# CPU scheduling pressure: threads waiting for a core (single instant sample).
try {
    $cpuQueue = [math]::Round(
        (Get-Counter '\System\Processor Queue Length' -MaxSamples 1 -ErrorAction Stop).CounterSamples[0].CookedValue, 0)
} catch {
    $cpuQueue = $null
}

# CPU delivered performance vs base clock (driver-free throttle signal).
# % Processor Performance >100 = boosting above base; ~66 = clamped to ~1.6GHz.
# Sample ~5s and keep the MAX to tell a real clamp from ordinary idle downclock.
try {
    $perfSamples = (Get-Counter '\Processor Information(_Total)\% Processor Performance' -SampleInterval 1 -MaxSamples 5 -ErrorAction Stop).CounterSamples | ForEach-Object { $_.CookedValue }
    $perfAvg = [math]::Round((($perfSamples | Measure-Object -Average).Average), 0)
    $perfMax = [math]::Round((($perfSamples | Measure-Object -Maximum).Maximum), 0)
    $perfFreq = [math]::Round((Get-Counter '\Processor Information(_Total)\Processor Frequency' -MaxSamples 1 -ErrorAction Stop).CounterSamples[0].CookedValue, 0)
} catch {
    $perfAvg = $null; $perfMax = $null; $perfFreq = $null
}

# Page file usage.
$pf = Get-CimInstance Win32_PageFileUsage -ErrorAction SilentlyContinue | Select-Object -First 1
if ($pf) {
    $pfAlloc   = $pf.AllocatedBaseSize
    $pfCurrent = $pf.CurrentUsage
    $pfPeak    = $pf.PeakUsage
} else {
    $pfAlloc = $null; $pfCurrent = $null; $pfPeak = $null
}

# Single process snapshot, reused below for consistency.
$procs = Get-Process

$stats = [ordered]@{
    schema_version   = 3
    timestamp        = $Timestamp
    cpu_load_pct     = [math]::Round($cpuLoad, 1)
    cpu_queue_length = $cpuQueue
    cpu_perf = [ordered]@{
        proc_performance_pct_avg = $perfAvg
        proc_performance_pct_max = $perfMax
        frequency_mhz            = $perfFreq
    }
    process_count    = $procs.Count
    ram = [ordered]@{
        total_gb  = [math]::Round($ramTotal / 1MB, 2)
        free_gb   = [math]::Round($ramFree  / 1MB, 2)
        used_pct  = [math]::Round(($ramTotal - $ramFree) / $ramTotal * 100, 1)
    }
    commit = [ordered]@{
        limit_gb = [math]::Round($commitLimit / 1MB, 2)
        used_gb  = [math]::Round($commitUsed  / 1MB, 2)
        used_pct = [math]::Round($commitUsed / $commitLimit * 100, 1)
    }
    pagefile = [ordered]@{
        allocated_mb = $pfAlloc
        current_mb   = $pfCurrent
        peak_mb      = $pfPeak
    }
    top_cpu_processes = @(
        $procs | Sort-Object CPU -Descending | Select-Object -First 10 |
        ForEach-Object {
            [ordered]@{
                name   = $_.Name
                cpu_s  = [math]::Round($_.CPU, 1)
                ram_mb = [math]::Round($_.WorkingSet / 1MB, 1)
            }
        }
    )
    top_ram_processes = @(
        $procs | Sort-Object WorkingSet -Descending | Select-Object -First 10 |
        ForEach-Object {
            [ordered]@{
                name   = $_.Name
                ram_mb = [math]::Round($_.WorkingSet / 1MB, 1)
                cpu_s  = [math]::Round($_.CPU, 1)
            }
        }
    )
    disks = @(
        Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Used -gt 0 } |
        ForEach-Object {
            [ordered]@{
                drive    = $_.Name
                used_gb  = [math]::Round($_.Used / 1GB, 2)
                free_gb  = [math]::Round($_.Free / 1GB, 2)
                used_pct = [math]::Round($_.Used / ($_.Used + $_.Free) * 100, 1)
            }
        }
    )
    system_errors_last_hour = (
        Get-EventLog -LogName System -EntryType Error -After (Get-Date).AddHours(-1) -ErrorAction SilentlyContinue
    ).Count
    app_errors_last_hour = (
        Get-EventLog -LogName Application -EntryType Error -After (Get-Date).AddHours(-1) -ErrorAction SilentlyContinue
    ).Count
}

$stats | ConvertTo-Json -Depth 5 | Out-File -FilePath $OutputFile -Encoding UTF8

# Prune files older than 30 days (longer window = better trend analysis).
Get-ChildItem $OutputDir -Filter "*.json" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item -Force
