# Install — Marsh PC Monitor

From a Windows PowerShell prompt at the repo root:

```powershell
.\app\install\install.ps1 -Publish
```

This will:

1. Publish `PcMonitor.exe` (self-contained, single file, win-x64).
2. Copy it to `%LocalAppData%\PcMonitor\`.
3. Copy `diagnose.ps1`, `live-probe.ps1`, `collect-stats.ps1` to `Documents\SysLogs\scripts\`.
4. Create a Start Menu shortcut.

To rebuild and reinstall after code changes, re-run the same command.

If you prefer a smaller framework-dependent build, add `-FrameworkDependent` to `publish.ps1`. You'll need the .NET 8 Desktop Runtime installed.
