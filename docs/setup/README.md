# Marsh PC System Monitor — Claude Companion Setup

## What This Does

Sets up your Windows machine to continuously log system stats so Claude
Code can act as an on-demand personal tech advisor. Two modes:

- **Hourly snapshots** — lightweight JSON files written automatically every
  hour, 7-day rolling window, Claude reads these to spot trends
- **Full diagnostic** — run manually when something feels off; Claude reads
  the full report and gives you prioritized findings

---

## File Structure

After setup, your logs live here:

```
C:\Users\dreux\Documents\SysLogs\
├── scripts\
│   ├── collect-stats.ps1       <- hourly collector (managed by task scheduler)
│   └── monitor-setup.ps1       <- setup script (run once)
├── hourly\
│   └── stats_YYYY-MM-DD_HH-mm.json   <- rolling 7-day snapshots
└── diagnostic_YYYY-MM-DD_HH-mm.txt   <- manual deep-dive runs
```

WSL equivalent root: `/mnt/c/Users/dreux/Documents/SysLogs/`

---

## One-Time Setup

1. Save all four files from this package to a temp folder on Windows
2. Open PowerShell as Administrator
3. Allow local scripts (one-time):
   ```powershell
   Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```
4. Navigate to the folder and run:
   ```powershell
   cd C:\path\to\where\you\saved\these
   .\monitor-setup.ps1
   ```
5. Confirm the scheduled task was created:
   ```powershell
   Get-ScheduledTask -TaskName "SysMonitor-HourlyCollect"
   ```

That's it. Hourly stats start collecting automatically.

---

## Running a Full Diagnostic

From PowerShell (or Claude Code via WSL):

**PowerShell direct:**
```powershell
powershell.exe -ExecutionPolicy Bypass -File "C:\Users\dreux\Documents\SysLogs\scripts\diagnose.ps1"
```

**From WSL / Claude Code:**
```bash
powershell.exe -ExecutionPolicy Bypass -File '/mnt/c/Users/dreux/Documents/SysLogs/scripts/diagnose.ps1'
```

Output lands at:
```
C:\Users\dreux\Documents\SysLogs\diagnostic_YYYY-MM-DD_HH-mm.txt
```

---

## Using Claude Code as Your Tech

### Run a fresh diagnostic and get analysis
```bash
# In Claude Code (WSL)
powershell.exe -ExecutionPolicy Bypass -File '/mnt/c/Users/dreux/Documents/SysLogs/scripts/diagnose.ps1'
# Then tell Claude: "read the latest diagnostic and give me your findings"
```

### Spot trends across recent hourly snapshots
Tell Claude Code:
> "Read the last 12 hourly stat files in /mnt/c/Users/dreux/Documents/SysLogs/hourly/ and tell me if anything looks like a pattern — memory creep, CPU spikes, error increases."

### Quick current status
```bash
cat $(ls /mnt/c/Users/dreux/Documents/SysLogs/hourly/*.json | tail -1)
```

### Suggested Claude Code prompts
- "Run diagnose.ps1 and give me the top 5 issues to address"
- "What processes are consistently high across my last 24 hours of stats?"
- "Are there any scheduled tasks or startup entries that look like bloat?"
- "My machine feels slow — run a diagnostic and tell me what's causing it"
- "Compare the last 24 hourly snapshots and flag anything anomalous"

---

## What Gets Collected

### Hourly (collect-stats.ps1)
- CPU load %
- RAM used/free/total
- Top 10 processes by CPU and RAM
- Disk usage per drive
- Error counts from System and Application event logs

### Full Diagnostic (diagnose.ps1)
Everything above, plus:
- Full system info (OS build, uptime, CPU specs)
- Top 25 processes each by CPU and RAM
- All registry startup programs (HKLM + HKCU)
- Running third-party services
- Non-Microsoft scheduled tasks
- Page file usage
- Temp folder sizes
- Last 24h of System and Application event log errors (with messages)

---

## Troubleshooting

**Scheduled task not running:**
```powershell
# Check task status
Get-ScheduledTask -TaskName "SysMonitor-HourlyCollect" | Select-Object State, LastRunTime, LastTaskResult

# Run manually
Start-ScheduledTask -TaskName "SysMonitor-HourlyCollect"
```

**Execution policy error:**
```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**No files appearing in hourly folder:**
Check that `collect-stats.ps1` exists at:
`C:\Users\dreux\Documents\SysLogs\scripts\collect-stats.ps1`
