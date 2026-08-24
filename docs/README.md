# pc-monitor — documentation index

## Tuning (`docs/tuning/`)

CPU power/thermal investigation on the Lenovo Legion 7 16IRX9 (i9-14900HX). These are
working diagnostic records, written in the order things were learned. **They deliberately
preserve corrections rather than rewriting history** — where a conclusion was later found
wrong, the original claim and the correction both stay, labelled. Read the status banners at
the top of each before trusting any individual claim.

| Doc | Subject | Status |
|---|---|---|
| [2026-07-16-power-clamp.md](tuning/2026-07-16-power-clamp.md) | Chronic sluggishness root-caused to ThrottleStop locking MSR 0x610 at the Battery profile's 17/20/12 on first unplug | **SOLVED, fix verified** |
| [2026-08-23-undervolt-retune.md](tuning/2026-08-23-undervolt-retune.md) | FIVR undervolting unlocked by a BIOS setting; retune plan and applied state | **Active** |

Raw evidence for both lives in [tuning/evidence/](tuning/evidence/) — HWiNFO CSVs,
a ThrottleStop log, and the screenshot set in `unplug_plug.docx`.

## Setup (`docs/setup/`)

[README.md](setup/README.md) — one-time setup of the hourly stats collector and the
on-demand full diagnostic, plus how to drive them from Claude Code.

## Design docs (`docs/superpowers/`)

Specs, plans and notes for the PcMonitor WPF app itself (`app/`).

---

## Scripts (`scripts/`)

| Script | Runs on | What it does |
|---|---|---|
| `ts-log-report.sh` | WSL/bash | Summarises ThrottleStop daily logs — temps, VID at matched load, throttle reasons, AC vs battery. The verification tool for tuning changes. |
| `collect-stats.ps1` | Windows | Hourly JSON snapshot collector (scheduled task) |
| `diagnose.ps1` | Windows | On-demand full system diagnostic |
| `live-probe.ps1` | Windows | Live sampling probe, includes CPU delivered-performance readout |
| `monitor-setup.ps1` | Windows | One-time setup — registers the scheduled task |
| `set-pagefile.ps1` | Windows | Pagefile configuration helper |

The PowerShell scripts are the **reference copies**. The live ones the scheduled task
actually runs are at `C:\Users\dreux\Documents\SysLogs\scripts\`. Edit here, copy across —
or copy back if they drift.
