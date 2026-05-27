# set-pagefile.ps1
# Run ELEVATED. Sets a fixed 24 GB page file on C: to raise the commit
# limit and relieve memory-commit pressure (the "feels sluggish" cause).
#
# Fixed size (initial = maximum) avoids fragmentation and unbounded
# growth on an already-full system drive.
#
# A REBOOT is required for the new size to take effect.

$ErrorActionPreference = 'Stop'
$targetMB = 24576   # 24 GB

Write-Host "=== Current page file (runtime) ===" -ForegroundColor Cyan
Get-CimInstance Win32_PageFileUsage |
    Select-Object Name, AllocatedBaseSize, CurrentUsage, PeakUsage |
    Format-Table -AutoSize

$cs = Get-WmiObject Win32_ComputerSystem -EnableAllPrivileges
Write-Host ("AutomaticManagedPagefile currently: {0}" -f $cs.AutomaticManagedPagefile)

if ($cs.AutomaticManagedPagefile) {
    $cs.AutomaticManagedPagefile = $false
    [void]$cs.Put()
    Write-Host "Disabled automatic page file management." -ForegroundColor Green
}

$pf = Get-WmiObject Win32_PageFileSetting | Select-Object -First 1
if ($pf) {
    $pf.InitialSize = $targetMB
    $pf.MaximumSize = $targetMB
    [void]$pf.Put()
    Write-Host ("Updated page file '{0}' to {1} MB fixed." -f $pf.Name, $targetMB) -ForegroundColor Green
} else {
    [void](Set-WmiInstance -Class Win32_PageFileSetting -Arguments @{
        Name        = 'C:\pagefile.sys'
        InitialSize = $targetMB
        MaximumSize = $targetMB
    })
    Write-Host ("Created page file C:\pagefile.sys at {0} MB fixed." -f $targetMB) -ForegroundColor Green
}

Write-Host "`n=== New page file setting (effective after reboot) ===" -ForegroundColor Cyan
Get-WmiObject Win32_PageFileSetting |
    Select-Object Name, InitialSize, MaximumSize |
    Format-Table -AutoSize

Write-Host "REBOOT REQUIRED for the new 24 GB page file to take effect." -ForegroundColor Yellow
Write-Host "Until you reboot, the commit limit stays at its current value." -ForegroundColor Yellow
