# pc-monitor

System monitoring and CPU power/thermal tuning for a Lenovo Legion 7 16IRX9 (i9-14900HX).

Two things live here:

- **`app/`** — PcMonitor, a WPF desktop app that reads the hourly stat snapshots and surfaces
  issues (thermal throttling, RAM pressure, runaway processes, disk/commit exhaustion).
- **`docs/` + `scripts/`** — the telemetry collectors and the tuning investigation records.

Start at **[docs/README.md](docs/README.md)** for the index.

## Quick reference

```bash
# Summarise today's ThrottleStop log (temps, VID, throttle reasons, AC vs battery)
./scripts/ts-log-report.sh

# Compare days
./scripts/ts-log-report.sh 2026-08-20 2026-08-22 2026-08-23
```

```powershell
# Full system diagnostic
powershell.exe -ExecutionPolicy Bypass -File 'C:\Users\dreux\Documents\SysLogs\scripts\diagnose.ps1'
```

## Data locations (Windows side)

| What | Where |
|---|---|
| Hourly stat snapshots | `C:\Users\dreux\Documents\SysLogs\hourly\` |
| Full diagnostics | `C:\Users\dreux\Documents\SysLogs\` |
| ThrottleStop daily logs | `C:\Users\dreux\Desktop\Logs\YYYY-MM-DD.txt` |
| ThrottleStop config | `C:\Users\dreux\Desktop\Utilities\ThrottleStop.ini` |
