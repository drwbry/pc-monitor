# live-probe.ps1 - Capture a 5-second live snapshot of what's actually
# stressing the system right now (useful when desktop feels sluggish).

$ts = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$out = "$env:USERPROFILE\Documents\SysLogs\live_$ts.txt"
"" | Out-File $out

function Section($title, $body) {
    $line = "=" * 70
    "`n$line`n  $title`n$line`n$body" | Tee-Object -Append -FilePath $out
}

$cores = (Get-CimInstance Win32_Processor | Measure-Object NumberOfLogicalProcessors -Sum).Sum
if (-not $cores) { $cores = 1 }

# 1. Per-process CPU % over 5 seconds (5 x 1s samples averaged)
$samples = Get-Counter '\Process(*)\% Processor Time' -SampleInterval 1 -MaxSamples 5
$avg = @{}
foreach ($s in $samples) {
    foreach ($cs in $s.CounterSamples) {
        $name = $cs.InstanceName
        if (-not $name -or $name -in @('_total','idle')) { continue }
        if (-not $avg.ContainsKey($name)) { $avg[$name] = @{ Sum = 0.0; N = 0 } }
        $avg[$name].Sum += $cs.CookedValue
        $avg[$name].N += 1
    }
}
$top = $avg.GetEnumerator() | ForEach-Object {
    [PSCustomObject]@{
        Process = $_.Key
        'Avg CPU %' = [math]::Round(($_.Value.Sum / $_.Value.N) / $cores, 2)
    }
} | Where-Object { $_.'Avg CPU %' -gt 0.1 } | Sort-Object 'Avg CPU %' -Descending | Select-Object -First 20

Section "TOP PROCESSES BY LIVE CPU % (avg over 5s, $cores logical cores)" (
    ($top | Format-Table -AutoSize | Out-String).TrimEnd()
)

# 2. CPU queue length (threads waiting for a core)
$queue = Get-Counter '\System\Processor Queue Length' -SampleInterval 1 -MaxSamples 3
$queueVals = $queue.CounterSamples | ForEach-Object { $_.CookedValue }
$queueAvg = [math]::Round((($queueVals | Measure-Object -Average).Average), 2)
$threshold = $cores * 2
Section "CPU PRESSURE" (@"
Processor Queue Length samples : $($queueVals -join ', ')
Average                        : $queueAvg
Threshold (2 x logical cores)  : $threshold
Interpretation                 : queue > threshold means threads are waiting for CPU even though no single process looks hot
"@)

# 2b. CPU delivered performance vs base clock (driver-free throttle signal)
$perfSamples = (Get-Counter '\Processor Information(_Total)\% Processor Performance' -SampleInterval 1 -MaxSamples 5).CounterSamples | ForEach-Object { $_.CookedValue }
$perfAvg = [math]::Round((($perfSamples | Measure-Object -Average).Average), 0)
$perfMax = [math]::Round((($perfSamples | Measure-Object -Maximum).Maximum), 0)
$perfFreq = [math]::Round((Get-Counter '\Processor Information(_Total)\Processor Frequency' -MaxSamples 1).CounterSamples[0].CookedValue, 0)
Section "CPU DELIVERED PERFORMANCE (vs base clock)" (@"
% Processor Performance avg : $perfAvg
% Processor Performance max : $perfMax
Reported frequency (MHz)    : $perfFreq
Interpretation              : >100 = boosting above base (healthy). Max well below ~100 while the queue is high = CPU clamped below base clock (e.g. BD PROCHOT throttle).
"@)

# 3. Disk queue length and idle time on physical disks
$disk = Get-Counter '\PhysicalDisk(*)\Avg. Disk Queue Length','\PhysicalDisk(*)\% Idle Time' -SampleInterval 1 -MaxSamples 3
$diskAgg = @{}
foreach ($s in $disk) {
    foreach ($cs in $s.CounterSamples) {
        if ($cs.InstanceName -eq '_total') { continue }
        $key = $cs.InstanceName + '|' + (($cs.Path -split '\\')[-1])
        if (-not $diskAgg.ContainsKey($key)) { $diskAgg[$key] = @() }
        $diskAgg[$key] += $cs.CookedValue
    }
}
$diskRows = $diskAgg.GetEnumerator() | ForEach-Object {
    $parts = $_.Key -split '\|'
    [PSCustomObject]@{
        Disk    = $parts[0]
        Counter = $parts[1]
        Avg     = [math]::Round((($_.Value | Measure-Object -Average).Average), 2)
    }
} | Sort-Object Disk, Counter
Section "DISK PRESSURE (3 samples averaged)" (($diskRows | Format-Table -AutoSize | Out-String).TrimEnd())

# 4. Memory pressure + hard page faults
$mem = Get-Counter '\Memory\Available MBytes','\Memory\Pages Input/sec','\Memory\% Committed Bytes In Use' -SampleInterval 1 -MaxSamples 3
$memRows = $mem[-1].CounterSamples | ForEach-Object {
    [PSCustomObject]@{
        Metric = ($_.Path -split '\\')[-1]
        Value  = [math]::Round($_.CookedValue, 2)
    }
}
Section "MEMORY PRESSURE" (($memRows | Format-Table -AutoSize | Out-String).TrimEnd() + "`nPages Input/sec > 0 means hard page faults (paging from disk) - real stutter cause.")

# 5. Shell + Start menu + search processes - are any of them hot or huge?
$shell = Get-Process StartMenuExperienceHost, SearchHost, SearchIndexer, ShellExperienceHost, explorer, dwm, dllhost, RuntimeBroker -ErrorAction SilentlyContinue |
    Select-Object Name, Id,
        @{N='CPU s'; E={[math]::Round($_.CPU,1)}},
        @{N='RAM MB'; E={[math]::Round($_.WorkingSet/1MB,1)}},
        @{N='Threads'; E={$_.Threads.Count}},
        @{N='Handles'; E={$_.HandleCount}} |
    Sort-Object Name
Section "SHELL / START MENU / SEARCH PROCESSES" (($shell | Format-Table -AutoSize | Out-String).TrimEnd())

# 6. Windows Search service indexing state
$wsearch = Get-Service WSearch -ErrorAction SilentlyContinue
$indexerCounter = $null
try {
    $indexerCounter = Get-Counter '\Search Indexer(*)\Documents Filtered','\Search Gathering Manager(*)\Active Connections','\Search Indexer(*)\Documents in Index Persistence Queue' -ErrorAction SilentlyContinue
} catch {}
$wsBlock = "WSearch status : $($wsearch.Status), StartType: $($wsearch.StartType)"
if ($indexerCounter) {
    $rows = $indexerCounter.CounterSamples | ForEach-Object {
        [PSCustomObject]@{
            Counter = ($_.Path -split '\\')[-1]
            Value   = $_.CookedValue
        }
    }
    $wsBlock += "`n" + (($rows | Format-Table -AutoSize | Out-String).TrimEnd())
}
Section "WINDOWS SEARCH / INDEXER" $wsBlock

# 7. Defender real-time scan state (active scan = guaranteed stutter)
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    $mpBlock = @"
Real-time protection : $($mp.RealTimeProtectionEnabled)
Currently scanning   : $(if ($mp.QuickScanOverdue -or $mp.FullScanOverdue) { 'Overdue (may trigger soon)' } else { 'No scan overdue' })
Last quick scan      : $($mp.QuickScanStartTime) -> $($mp.QuickScanEndTime)
Last full scan       : $($mp.FullScanStartTime) -> $($mp.FullScanEndTime)
Signatures           : $($mp.AntivirusSignatureLastUpdated)
"@
    # Sample MsMpEng live
    $mpCpu = Get-Counter '\Process(MsMpEng)\% Processor Time','\Process(NisSrv)\% Processor Time' -SampleInterval 1 -MaxSamples 3 -ErrorAction SilentlyContinue
    if ($mpCpu) {
        $mpRows = $mpCpu[-1].CounterSamples | ForEach-Object {
            [PSCustomObject]@{
                Process = $_.InstanceName
                'Live CPU %' = [math]::Round($_.CookedValue / $cores, 2)
            }
        }
        $mpBlock += "`n--- Defender process live CPU ---`n" + (($mpRows | Format-Table -AutoSize | Out-String).TrimEnd())
    }
} catch {
    $mpBlock = "Get-MpComputerStatus failed: $($_.Exception.Message)"
}
Section "WINDOWS DEFENDER LIVE" $mpBlock

# 8. NVIDIA processes (driver-related stutter is common)
$nv = Get-Process nvcontainer, NVDisplay.Container, nvsphelper64, NVIDIA*, nvidia-share -ErrorAction SilentlyContinue |
    Select-Object Name, Id,
        @{N='CPU s'; E={[math]::Round($_.CPU,1)}},
        @{N='RAM MB'; E={[math]::Round($_.WorkingSet/1MB,1)}} |
    Sort-Object Name
Section "NVIDIA SERVICE PROCESSES" (($nv | Format-Table -AutoSize | Out-String).TrimEnd())

# 9. Any scheduled task currently RUNNING (not just registered)
$running = Get-ScheduledTask | Where-Object { $_.State -eq 'Running' } |
    Select-Object TaskName, TaskPath, State |
    Sort-Object TaskPath, TaskName |
    Format-Table -AutoSize -Wrap | Out-String
Section "SCHEDULED TASKS CURRENTLY RUNNING" $running.TrimEnd()

# 10. SysMain (Superfetch) and other prefetch state - can spike during shell interactions
$sysmain = Get-Service SysMain, BITS, wuauserv, DiagTrack, dmwappushservice -ErrorAction SilentlyContinue |
    Select-Object Name, Status, StartType |
    Format-Table -AutoSize | Out-String
Section "BACKGROUND TELEMETRY / PREFETCH SERVICES" $sysmain.TrimEnd()

# 11. Recent stutter-relevant Application/System errors (last 1 hour)
try {
    $sysErr = Get-EventLog -LogName System -EntryType Error,Warning -After (Get-Date).AddHours(-1) -Newest 15 -ErrorAction Stop |
        Select-Object TimeGenerated, EntryType, Source,
            @{N='Message'; E={ $_.Message.Substring(0,[Math]::Min(180,$_.Message.Length)) }} |
        Format-Table -AutoSize -Wrap | Out-String
} catch { $sysErr = "(no recent system events)" }
Section "RECENT SYSTEM EVENTS (LAST 1H)" $sysErr.TrimEnd()

Write-Host "`nLive probe saved: $out" -ForegroundColor Cyan
Write-Host "WSL: /mnt/c/Users/$env:USERNAME/Documents/SysLogs/live_$ts.txt" -ForegroundColor Cyan
