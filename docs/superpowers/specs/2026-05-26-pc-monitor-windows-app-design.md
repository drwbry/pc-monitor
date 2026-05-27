# Marsh PC Monitor — Windows App Design

**Date:** 2026-05-26
**Status:** Reviewed and build-handoff ready

---

## Summary

Convert the existing PowerShell-based `pc-monitor` toolkit into a lightweight Windows desktop app: a focused "cockpit" the user opens when their PC feels off. The app shows live system health, flags issues via a small built-in rule engine, and orchestrates the existing PowerShell capture scripts to produce log files that Claude Code (in WSL) analyzes for root cause.

The app is the **"where do I look?"** layer. Claude Code remains the **"why is it happening?"** layer.

## Goals

- Single window that answers "is anything wrong right now?" in under two seconds.
- Surface issues the user cares about (thermal throttle, RAM pressure, sustained CPU hogs, disk space, event-log spikes) as opinionated cards without requiring threshold tuning.
- One-click capture flow that ends with a ready-to-paste Claude Code prompt on the clipboard.
- Coexist with the current `monitor-setup.ps1` scheduled task — the app does not replace background hourly collection.
- Lightweight in practice: sub-1% CPU when open, near-zero footprint when closed, fast cold start.

## Non-Goals (v1)

- Not a Task Manager / HWiNFO replacement. Live tiles are minimal and focused.
- No local trend analysis or anomaly detection beyond simple threshold rules. Claude Code does the deep analysis.
- No settings UI, no threshold tuning, no per-rule on/off toggles.
- No tray mode in v1 (plumbing is structured to allow it later).
- No notifications, toasts, or sounds. Strictly pull-based.
- No auto-update, telemetry, crash reporting, or multi-machine sync.
- No dGPU tiles or rules in v1 (deferrable to v1.1).
- No rewrite of the PowerShell scripts. The app orchestrates them.

## Tech Stack

- **WPF on .NET 8** for the desktop app.
- **Target framework:** `net8.0-windows10.0.19041.0` with `<UseWPF>true</UseWPF>`, nullable enabled, and Windows-only APIs isolated in `PcMonitor.Core`.
- **CommunityToolkit.Mvvm** for view models/commands. Keep MVVM thin and boring; do not introduce a larger UI framework.
- **LibreHardwareMonitorLib** (MIT) for CPU package temp, per-core temps, throttle-related sensors when available, and other hardware sensors. Same family of approach used by HWiNFO, Afterburner, Legion Toolkit, OpenHardwareMonitor.
- **`System.Diagnostics.PerformanceCounter`** package plus **WMI / CIM** for CPU%, RAM, disk, process listings, and event log queries.
- **xUnit** for `Core` unit tests.

### Build assumptions Claude Code should honor

- The WPF app is Windows-only. It can be authored from WSL, but `dotnet build/publish` and manual smoke testing should run on Windows PowerShell unless the local toolchain proves WPF publishing works from the current shell.
- Keep the app non-admin by default. Anything that requires elevation must degrade gracefully or be an explicit one-time action; do not mark the main app manifest `requireAdministrator`.
- The existing PowerShell scripts remain the diagnostic workhorses. The app may copy/install them, but should not rewrite their collection logic as part of v1.
- User-facing paths must be built from `%USERPROFILE%` / `Environment.SpecialFolder.MyDocuments`, never hard-coded to `dreux`, except examples in this design doc.

## Section 1 — Architecture & Project Layout

### Solution shape

```text
/home/dreux/projects/pc-monitor/
├── files/                              ← existing PowerShell scripts (unchanged)
└── app/
    ├── PcMonitor.sln
    ├── src/
    │   ├── PcMonitor.Core/             ← class library, no UI deps
    │   │   ├── Models/                 ← SensorSnapshot, IssueState, CaptureResult, hourly DTOs
    │   │   ├── Sensors/                ← LibreHardwareMonitorLib + PerfCounters + WMI
    │   │   ├── Issues/                 ← rule definitions + IssueEvaluator
    │   │   ├── History/                ← reads SysLogs\hourly\*.json + FileSystemWatcher
    │   │   └── Capture/                ← spawns powershell.exe, streams stdout
    │   └── PcMonitor.App/              ← WPF, MVVM
    │       ├── ViewModels/
    │       ├── Views/                  ← Cockpit window, capture modal
    │       └── App.xaml.cs             ← startup, single-instance guard, tray plumbing (disabled v1)
    └── tests/
        └── PcMonitor.Core.Tests/       ← xUnit tests for rules engine + capture service
```

`Core` has zero WPF references, keeping the rules engine and capture orchestrator testable without a window. `App` is the thin WPF shell wired to `Core` via view models.

### Runtime model

- **UI thread:** WPF dispatcher, idle most of the time.
- **Sensor poller:** background timer, 1 Hz tick. Each tick reads CPU / RAM / temps / throttle / top processes, runs the issue evaluator, then pushes immutable snapshots/results to the UI via dispatcher. If a tick is still running when the next tick arrives, skip the overlapping tick.
- **History reader:** loads existing hourly JSONs from `SysLogs\hourly\` at app open; `FileSystemWatcher` keeps the sparkline footer fresh while the window is open.
- **Capture service:** invokes `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>` as a child process, streams stdout/stderr to the capture modal, then surfaces the resulting file path.

### Core model contracts

Implementation can refine property names, but preserve these concepts so rules, UI, and tests have a stable contract:

```csharp
public sealed record SensorSnapshot(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? CpuPackageTempC,
    bool? IsThrottling,
    double RamUsedGb,
    double RamTotalGb,
    double FreePhysicalRamPercent,
    double? CommitUsedPercent,
    double? PagefileUsedPercent,
    double? DiskQueueLength,
    double? DriveCFreeGb,
    int? EventErrorsLast5Minutes,
    int? EventErrorsThisHour,
    IReadOnlyList<ProcessSample> TopProcesses);

public sealed record ProcessSample(
    int ProcessId,
    string Name,
    double CpuPercent,
    double RamMb);

public sealed record IssueState(
    string RuleId,
    IssueSeverity Severity,
    string Title,
    string Detail,
    DateTimeOffset FirstSeen,
    IReadOnlyDictionary<string, double?> Metrics);
```

Use `null` for unavailable sensors, not sentinel values. Rules that require missing data simply do not fire.

### Locations

- **Binary:** `%LocalAppData%\PcMonitor\PcMonitor.exe`. Start Menu shortcut. No `Program Files`, no admin needed for the app itself.
- **Data:** unchanged — `%USERPROFILE%\Documents\SysLogs\` remains the source of truth. The app reads from there; the existing scripts write to there. Claude Code's current paths keep working.
- **Companion scripts:** `diagnose.ps1` and `live-probe.ps1` must exist at `%USERPROFILE%\Documents\SysLogs\scripts\` for capture buttons to work. The app installer should copy them there from the repo's `files/` folder, because the current `monitor-setup.ps1` only installs `collect-stats.ps1`.
- **LibreHardwareMonitor kernel driver:** hardware temps may require driver/elevated access depending on the machine. v1 should attempt LHM initialization, catch failures, and disable temp/throttle rules with a banner if unavailable. A one-time "Enable temperature sensors" elevated helper/button is allowed, but the rest of v1 must work without it.

### Single-instance behavior

Named mutex on startup. Launching the shortcut a second time brings the existing window to the foreground instead of opening a duplicate. Use a small local IPC signal (named pipe or equivalent) from the second process to the first process; a mutex alone is not enough to activate the existing window reliably.

---

## Section 2 — The Cockpit (UI, issue engine, explainer)

### Window layout

Approximately 960 × 640, resizable, follows Windows light/dark theme.

```text
┌─────────────────────────────────────────────────────────────────┐
│  Marsh PC Monitor             ● All clear │ ▲ Issues │ ■ Problem│   ← top bar + health badge
├─────────────────────────────────────────────────────────────────┤
│  ▾ How to use this                                              │   ← collapsible explainer
│                                                                 │     (expanded on first run,
│  This is your "is anything wrong?" cockpit. Live tiles below    │      collapsed after)
│  show what's happening right now. Issue cards appear when       │
│  something crosses a threshold. When you want a deeper look:    │
│                                                                 │
│   1. Click Capture Diagnostic (full snapshot, ~10–20s) or       │
│      Capture Live Probe (5s trace of what's hammering CPU).     │
│   2. When it finishes, hit "Copy Claude prompt" — it puts a     │
│      ready-to-paste prompt with the file path on your clipboard.│
│   3. Paste into Claude Code in WSL and let it analyze.          │
│                                                                 │
│  Files land in Documents\SysLogs\ alongside the hourly          │
│  snapshots the scheduled task is already collecting.            │
├─────────────────────────────────────────────────────────────────┤
│  ISSUES (only visible when triggered)                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ● Thermal throttle active                  for 42s       │   │
│  │   CPU package at 97°C; PROCHOT detected.                 │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ▲ chrome.exe high CPU                      14 min, 38%   │   │
│  └──────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│  LIVE                                                           │
│  CPU 47%      RAM 22.4/64 GB     Pkg Temp 73°C    C: 412 GB     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Top processes                  CPU %     RAM            │   │
│  │  chrome.exe                     38.2      2.1 GB         │   │
│  │  Code.exe                        6.4      1.4 GB         │   │
│  │  …                                                       │   │
│  └──────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────┤
│  [  Capture Diagnostic  ]   [  Capture Live Probe (5s)  ]       │
├─────────────────────────────────────────────────────────────────┤
│  24h trends:  CPU ▁▂▂▃▅▃▂▁▁▂  RAM ▂▂▃▃▄▄▅▅▅▆  Errors ▁▁▁▁▁▃▁    │
└─────────────────────────────────────────────────────────────────┘
```

### Explainer panel

A collapsible "How to use this" section, expanded on first run, collapsed thereafter. State persists in `%LocalAppData%\PcMonitor\settings.json` so the behavior is explicit and easy to inspect. Copy is roughly the text shown above; final wording can be tuned during implementation.

### Health badge

Computed once per tick from the active issue list. Red if any red rule fires, Yellow if any yellow rule fires, Green otherwise.

### Issue cards

Rendered only when a rule fires. Each card: colored marker (red dot / yellow triangle) + one-line title + one-line detail. Sorted Red-first, then by how long the rule has been firing. Clicking a card expands it to show a sparkline of the underlying metric over the last 5 minutes.

### Issue engine

Rules live in `PcMonitor.Core.Issues` as small classes implementing:

```csharp
public interface IIssueRule
{
    IssueState? Evaluate(SensorSnapshot current, IReadOnlyList<SensorSnapshot> recent);
}
```

`recent` is a rolling 5-minute in-memory buffer of snapshots, so "sustained for N seconds" thresholds don't need disk reads. The `IssueEvaluator` runs every rule per tick and emits the active set. Adding a rule later means one new class plus registering it in the evaluator's rule list. Pure logic, easy to unit-test.

Rule identity is stable: each rule has a constant `RuleId` used by tests, UI expansion state, and logs. `FirstSeen` should be preserved while a rule remains active and reset only after the rule clears.

### v1 ruleset

**Red — "something is wrong right now":**

| Rule | Threshold |
|------|-----------|
| Thermal throttle active | `IsThrottling` true (LHM exposes via package temp / PROCHOT) |
| CPU package temp sustained high | ≥ 95°C for ≥ 30 s |
| RAM near exhaustion | Committed bytes / commit limit ≥ 95% (system is paging) |
| Drive C: critically full | Free space < 5 GB |
| Runaway process | Single process > 50% CPU for ≥ 5 min (normalized 0–100% across all logical cores, matching `live-probe.ps1`) |
| Event log spike | ≥ 10 errors in last 5 min |

**Yellow — "worth a look":**

| Rule | Threshold |
|------|-----------|
| CPU package temp elevated | ≥ 85°C for ≥ 1 min |
| RAM pressure | Free physical RAM < 15% of total (distinct from Red commit rule above — catches working-set pressure before paging starts) |
| Sustained CPU hog | Single process > 30% CPU for ≥ 10 min |
| Memory hog | Single process > 4 GB RAM |
| Drive C: getting full | Free space < 20 GB |
| Pagefile pressure | Pagefile usage > 50% |
| Event log uptick | Errors this hour ≥ 2× rolling 24 h hourly average |
| Disk queue elevated | Sustained queue length > 4 |

Implementation notes:

- Treat `IsThrottling` as best-effort. If LHM does not expose a reliable throttle/PROCHOT sensor on the target CPU, temp-based rules still work and the explicit throttle rule remains disabled.
- Event log counts should not be queried every 1 Hz tick. Cache them on a slower cadence, roughly every 30-60 seconds, and reuse the last value in intervening snapshots.
- `Event log uptick` uses the existing hourly JSON files. If fewer than 6 hourly files exist, suppress this rule to avoid noisy averages.
- `Pagefile pressure` is `CurrentUsage / AllocatedBaseSize` from `Win32_PageFileUsage` when available. If `AllocatedBaseSize` is zero or missing, suppress the rule.
- `Disk queue elevated` is sustained for at least 60 seconds. Disk queue can be noisy, so avoid firing on a single sample.

### Live tiles

- CPU % (overall, derived from `\Processor(_Total)\% Processor Time`)
- RAM used / total
- CPU package temp (via LHM)
- C: free space
- Top processes table (top 5 by CPU; one row each: name, CPU %, RAM). Refreshes at 1 Hz.

Per-process CPU must be a live delta/interval measurement normalized to `0-100%` across all logical cores, matching `live-probe.ps1`. Do not sort by cumulative `Process.TotalProcessorTime` or the app will show old long-running processes as "hot."

### Capture result modal

Shown when a capture script exits successfully:

```text
┌──────────────────────────────────────────────────────────────┐
│  Diagnostic captured                                         │
│                                                              │
│  File: diagnostic_2026-05-26_14-32.txt                       │
│  Location: C:\Users\dreux\Documents\SysLogs\                 │
│                                                              │
│  Suggested Claude Code prompt:                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Read /mnt/c/Users/dreux/Documents/SysLogs/             │  │
│  │ diagnostic_2026-05-26_14-32.txt and give me the top    │  │
│  │ 5 issues to address.                                   │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  [ Copy Claude prompt ]  [ Open in Explorer ]  [ Close ]     │
└──────────────────────────────────────────────────────────────┘
```

The **"Copy Claude prompt"** button is the headline action: one click, paste into Claude Code, done. Prompt template differs per capture kind:

- **Diagnostic:** `Read <wsl-path> and give me the top 5 issues to address.`
- **Live Probe:** `Read <wsl-path> and tell me what's hammering the CPU right now.`

### Polling rate

1 Hz default. LHM caches between reads, perf counters are cheap. Sub-1% CPU while the window is open.

---

## Section 3 — Capture orchestration, install, error handling, scope

### Capture orchestration

- `CaptureService.RunAsync(CaptureKind kind, IProgress<CaptureLine> progress, CancellationToken ct)` spawns `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>` with redirected stdout/stderr, streams lines to the progress modal, and returns a `CaptureResult`.
- Script paths resolve to `%USERPROFILE%\Documents\SysLogs\scripts\<script>.ps1`. If missing, the modal shows: "Scripts not installed — re-run `app/install/install.ps1` or copy the scripts from the repo's `files/` folder," with an "Open scripts folder" action.
- Only one capture at a time; the buttons disable while one is running. Live tiles keep updating in the background.
- After the script exits, the service scans `SysLogs\` for the newest matching file (`diagnostic_*.txt` for diagnostic captures, `live_*.txt` for live probe captures) created during the run window. That file is what the result modal references — more robust than parsing the script's own "output saved to…" line.
- Support cancellation from the progress modal. Cancellation kills the child PowerShell process tree and reports a cancelled state without showing the result prompt.

```csharp
public sealed record CaptureResult(
    CaptureKind Kind,
    bool Success,
    bool Cancelled,
    int? ExitCode,
    string? WindowsPath,
    string? WslPath,
    string? StdErr);

public sealed record CaptureLine(
    DateTimeOffset Timestamp,
    bool IsStdErr,
    string Text);
```

Convert Windows paths to WSL paths with a helper that handles drive letters generically:

```text
C:\Users\<user>\Documents\SysLogs\diagnostic_x.txt
→ /mnt/c/Users/<user>/Documents/SysLogs/diagnostic_x.txt
```

### Capture kinds in v1

| Kind | Script | Typical duration | Output |
|------|--------|------------------|--------|
| Diagnostic | `diagnose.ps1` | ~10–20 s | `diagnostic_YYYY-MM-DD_HH-mm.txt` |
| Live Probe | `live-probe.ps1` | ~5 s | `live_YYYY-MM-DD_HH-mm-ss.txt` |

### Install & distribution

- **Build:** publish win-x64. Prefer self-contained for handoff reliability:
  `dotnet publish app/src/PcMonitor.App/PcMonitor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`.
- **Framework-dependent option:** acceptable only if the install script verifies `Microsoft.WindowsDesktop.App 8.x` is installed and gives a clear download/install message when missing. Windows 11 does not guarantee the .NET 8 Desktop Runtime is present.
- **Installer:** a tiny `app/install/install.ps1` copies `PcMonitor.exe` to `%LocalAppData%\PcMonitor\`, copies `files/diagnose.ps1`, `files/live-probe.ps1`, and `files/collect-stats.ps1` to `%USERPROFILE%\Documents\SysLogs\scripts\`, and creates a Start Menu shortcut. No MSI, no admin. Mirrors the philosophy of the existing `monitor-setup.ps1`.
- **Scheduled hourly collection:** preserve the existing `monitor-setup.ps1` path. The app installer may copy scripts, but it should not silently register or modify the scheduled task unless the user runs the setup script/action explicitly.
- **LibreHardwareMonitor driver:** best-effort temp support; failure is documented in the install README and represented in-app as a degraded sensor state, not a crash.
- **Updates:** rebuild and re-run `install.ps1`. No auto-update for v1.

### Error handling — degrade, don't crash

| Failure | Behavior |
|---------|----------|
| LHM driver fails to install/load | Temp tile and thermal-related issue rules disable; small banner explains; rest of cockpit works. |
| `SysLogs\hourly\` missing or empty | Sparkline footer hides; everything else works. |
| Capture script missing | Capture modal explains which script is missing and offers "Open scripts folder". |
| Capture script returns non-zero | Result modal shows stderr/output summary; no "Copy prompt" button; "Open scripts folder" link instead. |
| Capture script writes benign stderr but exits 0 and output file exists | Treat as success, but show stderr in a collapsible details area. Exit code and output file presence are the primary success checks. |
| Sensor poller throws | Log to `%LocalAppData%\PcMonitor\log.txt`; skip the tick; keep polling. Never tear down the UI from a polling error. |
| Single-instance mutex contended | Foreground the existing window; second process exits silently. |
| Performance counter category missing/localized/unavailable | Mark that metric unavailable, log once per run, and keep the app responsive. |

### Testing

- **`PcMonitor.Core.Tests`** (xUnit):
  - Per-rule tests: feed each `IIssueRule` a sequence of fake `SensorSnapshot`s, assert firing / not-firing behavior across the threshold and sustained-duration boundaries.
  - `IssueEvaluator` tests: rolling buffer behavior, sustained-for-N-seconds timing accuracy, multi-rule prioritization for the health badge.
  - `CaptureService` tests using a fake `IProcessRunner` so PowerShell isn't required in CI.
  - Path conversion tests: Windows `C:\...` paths become `/mnt/c/...`; spaces are preserved.
  - Hourly history parser tests: schema v2 JSON from `collect-stats.ps1` loads, missing optional fields become null/defaults, malformed files are skipped with a log entry.
- **No automated UI tests in v1.** UI is thin enough to verify by hand on the target machine.
- **Manual smoke test:** on first build, run the app on Marsh PC, confirm: live tiles populate; temp sensors either work or show the degraded banner; "Capture Diagnostic" and "Capture Live Probe" each produce a file and surface the correct path; "Copy Claude prompt" puts a working prompt on the clipboard.

### Acceptance checklist for handoff

- App builds and publishes from the documented command.
- Cold launch opens the cockpit in under 2 seconds on the target PC.
- With `Documents\SysLogs\hourly\` absent, the app still opens and live tiles work.
- Capture buttons are disabled while a capture is running and re-enabled afterward.
- Diagnostic capture produces a `diagnostic_*.txt` path and a Claude prompt containing the correct `/mnt/c/...` path.
- Live probe capture produces a `live_*.txt` path and a Claude prompt containing the correct `/mnt/c/...` path.
- Killing or failing a capture does not crash the app.
- Unavailable LHM/temp sensors disable only temp/throttle tiles/rules.
- Single-instance launch activates the first window.
- Rule tests cover all red/yellow thresholds.

### v1 scope — what's OUT (YAGNI)

- **No settings pane / threshold tuning UI.** Rules are baked in; tuning requires a rebuild. Once we've lived with v1, we'll know which thresholds are wrong and add settings *then*.
- **No tray mode.** `App.xaml.cs` will be structured so a tray host can plug in later without restructuring.
- **No notifications / toasts / sounds.** Pull-based only. Tray mode (later) is the natural home for passive alerts.
- **No auto-update, telemetry, crash reporting, multi-machine sync.**
- **No built-in log analysis.** Claude Code's job, by design.
- **No dGPU tiles or rules.** LHM supports it; defer to v1.1 if usage shows it matters.
- **No PowerShell rewrite.** The scripts stay as the workhorse.

---

## Open Questions

None blocking implementation. Known implementation probes:

- Exact LHM sensor names for throttle/PROCHOT vary by CPU and may not exist; implement this as best-effort.
- Performance counter category names can vary by Windows configuration; use invariant APIs where practical and degrade otherwise.
- If self-contained WPF publish size is unacceptable after first build, switch to framework-dependent plus runtime check in `install.ps1`.
