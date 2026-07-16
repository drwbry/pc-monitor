# CPU Throttle Telemetry for Snapshots (schema v3)

**Date:** 2026-07-15
**Status:** Approved (design), pending implementation plan

## Problem

The machine (Legion i9-14900HX) suffers recurring, near-daily sluggishness while
CPU load and RAM look fine. Root cause was diagnosed live on 2026-07-15 as
**BD PROCHOT clamping the CPU to ~17W / ~1.6GHz — below the 2.4GHz base clock** —
while the die stays cool (~74°C) and the correct 330W adapter is connected. See
memory `project-bd-prochot-throttle`.

The existing telemetry could only show the *symptom indirectly*: e.g. the hourly
snapshot at 16:54 showed `cpu_queue_length = 28` while `cpu_load_pct = 13`. Nothing
in the snapshot records the actual delivered clock, so a throttle event cannot be
confirmed from the logs — it required live ThrottleStop/HWiNFO inspection.

**Goal:** make the hourly snapshot and the on-demand live probe record enough to
confirm "CPU was clamped below base clock" after the fact, so future "it's sluggish"
investigations are self-diagnosing from the logs.

## Constraint that shapes the design

LibreHardwareMonitor's ring0 driver is **blocked by Secure Boot** on this machine,
so the C# app already gets `null` from LHM for CPU temp/power/throttle sensors (it
falls back to ACPI thermal zone for temp). Therefore we **cannot** read package
watts or the PROCHOT/power-limit MSR bits without either a blocked driver or a
permanent 24/7 HWiNFO dependency.

**Decision:** capture the throttle **symptom only, driver-free**, via Windows
performance counters. Explicitly out of scope: throttle *reason* (BD PROCHOT vs
PL1), VRM temps, package watts.

## Signal & the idle-vs-throttle wrinkle

Signal: `\Processor Information(_Total)\% Processor Performance` — delivered
performance relative to the 2.4GHz base. `>100` = boosting above base (healthy);
`~66%` = clamped to ~1.6GHz. Companion: `\Processor Information(_Total)\Processor
Frequency`.

Wrinkle: `% Processor Performance` is *also* low at genuine idle, because cores
downclock on purpose. A single low reading does not prove throttling.

Resolution: sample briefly (~5×1s) and record the **max** as well as the average.
Even a lightly-loaded machine momentarily boosts *some* core above base; if the
**max** stays pinned low across the window, the CPU is clamped rather than idle.
Interpretation at review time (not baked into a fragile boolean): low
`proc_performance_pct_max` **combined with** an elevated `cpu_queue_length` = throttled
below base. Raw numbers are stored; interpretation stays with the reviewer.

## Changes

### 1. Hourly snapshot — `files/collect-stats.ps1`

- Sample `% Processor Performance` and `Processor Frequency` for ~5×1s.
- Add a `cpu_perf` object to the `$stats` hashtable:

```json
"cpu_perf": {
  "proc_performance_pct_avg": 84,
  "proc_performance_pct_max": 112,
  "frequency_mhz": 2108
}
```

- Bump `schema_version` from `2` to `3` (currently line 52).
- Cost: ~5s added to the hourly run.

### 2. Live probe — `files/live-probe.ps1`

- Add a "% Processor Performance / frequency" readout to the existing CPU PRESSURE
  section (near the Processor Queue Length block, ~line 44), so an on-demand probe
  shows the throttle number inline — that is the tool run when a problem is live.

### 3. C# consumer — backward compatible

The parser reads fields by name, so v2 files simply lack `cpu_perf`. Make the new
model data **optional/nullable** so historical v2 snapshots still parse.

- `app/src/PcMonitor.Core/Models/HourlyEntry.cs` — add nullable throttle fields
  (e.g. a nullable `CpuPerf` sub-record with avg/max/frequency).
- `app/src/PcMonitor.Core/History/HourlyJsonParser.cs` — parse `cpu_perf` if present,
  leave null if absent.
- `app/tests/PcMonitor.Core.Tests/History/HourlyJsonParserTests.cs` — keep the
  existing v2 fixture (asserts `cpu_perf` is null / absent parses cleanly) and add a
  v3 fixture asserting the new fields parse correctly.

### 4. Deployment

`files/*.ps1` are the git source of truth; the running copies live in
`C:\Users\dreux\Documents\SysLogs\scripts\`. After editing + committing the repo
copies, deploy by copying the two scripts to `SysLogs\scripts\` (same mechanism the
install flow uses).

## Out of scope

- Throttle reason (BD PROCHOT vs PL1 vs EDP), VRM/MOSFET temps, package watts —
  require a blocked driver or a permanent HWiNFO shared-memory dependency.
- Any derived/alerting boolean or threshold in the data itself.
- Changes to the C# live `SensorService` LHM path (unchanged; still Secure-Boot-limited).

## Success criteria

- A v3 hourly snapshot contains `cpu_perf` with plausible avg/max/frequency values.
- During a healthy period, `proc_performance_pct_max` reads ≥ ~100.
- During a BD PROCHOT clamp, `proc_performance_pct_max` reads well below base (~66)
  while `cpu_queue_length` is elevated — confirming the event from logs alone.
- Historical v2 snapshots still parse without error (nullable `cpu_perf`).
- The live probe prints the % Processor Performance / frequency line.
