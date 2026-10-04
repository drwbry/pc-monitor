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

## Game Mode (PcMonitor v1.1)

One button that closes memory-hungry background apps before gaming and brings them back after.
Open it from the cockpit's **Game Mode** button or the tray icon's **Start Game Mode...** item.
You see a list of what is running, all checked, with RAM per item. Uncheck anything you want to
keep, then **Confirm**. Ending Game Mode works the same way in reverse.

Apps you often use while gaming (Chrome, Discord, Spotify) appear in a separate "Also running"
section, **unchecked**. Tick them only when you want them closed too. In `gamemode.json` this is
`"SelectedByDefault": false`.

- The list lives in `%LocalAppData%\PcMonitor\gamemode.json` (written with defaults on first use).
  Edit it to add or remove apps. Item kinds are `App`, `Service` and `Command`.
- What was closed is saved in `gamemode-state.json`, so "End Game Mode" still works after a reboot.
- Apps are relaunched through `explorer.exe`, so they come back as normal (non-elevated) apps
  even though PcMonitor itself runs elevated.
- Services are stopped only. Their startup type is never changed, so a reboot is always a clean reset.
- A hardcoded protected list (ThrottleStop, Afterburner, Legion Toolkit, Steam and other launchers,
  G HUB, NVIDIA, audio, Defender, VPNs, PcMonitor) is never touched, whatever the JSON says.
- Ending WSL closes every Claude Code session running in WSL.

## Data locations (Windows side)

| What | Where |
|---|---|
| Hourly stat snapshots | `C:\Users\dreux\Documents\SysLogs\hourly\` |
| Full diagnostics | `C:\Users\dreux\Documents\SysLogs\` |
| ThrottleStop daily logs | `C:\Users\dreux\Desktop\Logs\YYYY-MM-DD.txt` |
| ThrottleStop config | `C:\Users\dreux\Desktop\Utilities\ThrottleStop.ini` |
