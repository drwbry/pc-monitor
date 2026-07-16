# CPU Throttle Telemetry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record a driver-free CPU-throttle signal (`% Processor Performance` + frequency) in the hourly snapshot and live probe so BD PROCHOT clamps (CPU pinned below base clock) are confirmable from logs alone.

**Architecture:** Add a `cpu_perf` object (avg/max % Processor Performance + frequency) to the PowerShell snapshot/probe via Windows performance counters; bump snapshot schema v2→v3; extend the C# `HourlyEntry` record + `HourlyJsonParser` with optional (nullable) fields so historical v2 files still parse.

**Tech Stack:** PowerShell 5.1 (`Get-Counter`), C# .NET 8 (`System.Text.Json`), xUnit + FluentAssertions.

## Global Constraints

- Snapshot schema version becomes exactly `3` (was `2`).
- New telemetry is **symptom-only, driver-free** — no throttle reason, VRM temps, or package watts (LHM/MSR is Secure-Boot-blocked on this machine).
- New C# fields MUST be nullable/optional; v2 files without `cpu_perf` MUST still parse (return nulls, no exception).
- Counter names verbatim: `\Processor Information(_Total)\% Processor Performance` and `\Processor Information(_Total)\Processor Frequency`.
- JSON field names verbatim: `cpu_perf.proc_performance_pct_avg`, `cpu_perf.proc_performance_pct_max`, `cpu_perf.frequency_mhz`.
- Repo `files/*.ps1` is the source of truth; deployed copies live in `C:\Users\dreux\Documents\SysLogs\scripts\`.
- C# tests run via **Windows** dotnet (project TFM is `net8.0-windows10.0.19041.0`; Linux dotnet cannot build it). Run from WSL through interop against the UNC path.

---

### Task 1: C# — optional `cpu_perf` fields in model + parser (TDD)

**Files:**
- Modify: `app/src/PcMonitor.Core/Models/HourlyEntry.cs`
- Modify: `app/src/PcMonitor.Core/History/HourlyJsonParser.cs`
- Test: `app/tests/PcMonitor.Core.Tests/History/HourlyJsonParserTests.cs`

**Interfaces:**
- Produces: `HourlyEntry` gains three trailing optional params `double? CpuProcPerfPctAvg = null, double? CpuProcPerfPctMax = null, double? CpuFrequencyMhz = null`. `HourlyJsonParser.Parse(string)` populates them from a `cpu_perf` object when present.

- [ ] **Step 1: Write the failing tests**

Add to `app/tests/PcMonitor.Core.Tests/History/HourlyJsonParserTests.cs` — a v3 fixture constant after the existing `ValidJson` constant (inside the class), and two `[Fact]` methods:

```csharp
    private const string V3Json = """
        {
          "schema_version": 3,
          "timestamp": "2026-07-15T21:00:00-04:00",
          "cpu_load_pct": 13.0,
          "cpu_queue_length": 28,
          "cpu_perf": { "proc_performance_pct_avg": 66.0, "proc_performance_pct_max": 68.0, "frequency_mhz": 1594.0 },
          "ram": { "total_gb": 31.71, "free_gb": 9.35, "used_pct": 70.5 }
        }
        """;

    [Fact]
    public void Parse_V3WithCpuPerf_PopulatesThrottleFields()
    {
        var entry = HourlyJsonParser.Parse(V3Json);
        entry.Should().NotBeNull();
        entry!.CpuProcPerfPctAvg.Should().Be(66.0);
        entry.CpuProcPerfPctMax.Should().Be(68.0);
        entry.CpuFrequencyMhz.Should().Be(1594.0);
    }

    [Fact]
    public void Parse_V2WithoutCpuPerf_ThrottleFieldsNull()
    {
        var entry = HourlyJsonParser.Parse(ValidJson);
        entry.Should().NotBeNull();
        entry!.CpuProcPerfPctAvg.Should().BeNull();
        entry.CpuProcPerfPctMax.Should().BeNull();
        entry.CpuFrequencyMhz.Should().BeNull();
    }
```

- [ ] **Step 2: Run tests to verify they fail (compile error — members don't exist)**

Run:
```bash
powershell.exe -NoProfile -Command "dotnet test '\\wsl.localhost\Ubuntu\home\dreux\projects\pc-monitor\app\tests\PcMonitor.Core.Tests\PcMonitor.Core.Tests.csproj'"
```
Expected: build FAILS — `'HourlyEntry' does not contain a definition for 'CpuProcPerfPctAvg'`. (First run also restores NuGet packages; may take a minute.)

- [ ] **Step 3: Add the nullable fields to the record**

Replace the body of `app/src/PcMonitor.Core/Models/HourlyEntry.cs` with:

```csharp
namespace PcMonitor.Core.Models;

public sealed record HourlyEntry(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? RamUsedGb,
    double? RamTotalGb,
    double? DriveCFreeGb,
    int? SystemErrorsLastHour,
    int? AppErrorsLastHour,
    double? CpuProcPerfPctAvg = null,
    double? CpuProcPerfPctMax = null,
    double? CpuFrequencyMhz = null);
```

- [ ] **Step 4: Parse `cpu_perf` in the parser**

In `app/src/PcMonitor.Core/History/HourlyJsonParser.cs`, immediately before the `return new HourlyEntry(...)` line (currently line 46), insert:

```csharp
            double? procPerfAvg = null, procPerfMax = null, freqMhz = null;
            if (root.TryGetProperty("cpu_perf", out var cpuPerf))
            {
                procPerfAvg = GetDouble(cpuPerf, "proc_performance_pct_avg");
                procPerfMax = GetDouble(cpuPerf, "proc_performance_pct_max");
                freqMhz = GetDouble(cpuPerf, "frequency_mhz");
            }

```

Then replace the existing `return` statement with:

```csharp
            return new HourlyEntry(ts.Value, cpuPct, ramUsed, ramTotal, driveCFree, sysErr, appErr,
                procPerfAvg, procPerfMax, freqMhz);
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
powershell.exe -NoProfile -Command "dotnet test '\\wsl.localhost\Ubuntu\home\dreux\projects\pc-monitor\app\tests\PcMonitor.Core.Tests\PcMonitor.Core.Tests.csproj'"
```
Expected: PASS — all tests, including the two new ones and the pre-existing v2 tests.

- [ ] **Step 6: Commit**

```bash
git add app/src/PcMonitor.Core/Models/HourlyEntry.cs app/src/PcMonitor.Core/History/HourlyJsonParser.cs app/tests/PcMonitor.Core.Tests/History/HourlyJsonParserTests.cs
git commit -m "core: parse optional cpu_perf throttle fields (schema v3, back-compat)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Hourly snapshot — emit `cpu_perf`, bump schema to v3

**Files:**
- Modify: `files/collect-stats.ps1`

**Interfaces:**
- Produces: hourly JSON now contains `"schema_version": 3` and a `cpu_perf` object with `proc_performance_pct_avg`, `proc_performance_pct_max`, `frequency_mhz` (numbers, or null on counter failure).

- [ ] **Step 1: Add the perf-counter sampling block**

In `files/collect-stats.ps1`, after the CPU queue block (ends line 36, before the `# Page file usage.` comment on line 38), insert:

```powershell
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
```

- [ ] **Step 2: Bump the schema version and add the `cpu_perf` object**

In `files/collect-stats.ps1`, change line 52 from `schema_version   = 2` to `schema_version   = 3`.

Then, inside the `$stats` hashtable, immediately after the `cpu_queue_length = $cpuQueue` line (line 55), insert:

```powershell
    cpu_perf = [ordered]@{
        proc_performance_pct_avg = $perfAvg
        proc_performance_pct_max = $perfMax
        frequency_mhz            = $perfFreq
    }
```

- [ ] **Step 3: Update the schema comment header**

In `files/collect-stats.ps1`, after the existing `# Schema v2:` comment block (ends line 8), add:

```powershell
#
# Schema v3: adds cpu_perf (% Processor Performance avg/max over ~5s + reported
# frequency) - a driver-free signal that confirms CPU clamping below base clock
# (e.g. BD PROCHOT), which load/RAM metrics cannot see.
```

- [ ] **Step 4: Verify the script produces valid v3 JSON**

Run (Windows PowerShell, from the repo copy):
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w files/collect-stats.ps1)"
```
Then inspect the newest hourly file:
```bash
ls -t /mnt/c/Users/dreux/Documents/SysLogs/hourly/*.json | head -1 | xargs cat | python3 -c "import sys,json; d=json.load(sys.stdin); print('schema', d['schema_version']); print('cpu_perf', d.get('cpu_perf'))"
```
Expected: `schema 3` and a `cpu_perf` dict with three numeric values (avg likely 80–120 on a healthy idle machine, max ≥ avg).

- [ ] **Step 5: Commit**

```bash
git add files/collect-stats.ps1
git commit -m "collect-stats: emit cpu_perf throttle signal, bump schema v2->v3

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Live probe — print delivered-performance readout

**Files:**
- Modify: `files/live-probe.ps1`

**Interfaces:**
- Consumes: the `Section` helper and `$cores` already defined in the script.
- Produces: a new "CPU DELIVERED PERFORMANCE" section in the live text output.

- [ ] **Step 1: Add the delivered-performance section**

In `files/live-probe.ps1`, immediately after the CPU PRESSURE `Section` call (ends line 49, before the `# 3. Disk queue` comment on line 51), insert:

```powershell
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
```

- [ ] **Step 2: Verify the probe prints the new section**

Run:
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w files/live-probe.ps1)"
```
Then check the newest live file contains the section:
```bash
ls -t /mnt/c/Users/dreux/Documents/SysLogs/live_*.txt | head -1 | xargs grep -A4 "CPU DELIVERED PERFORMANCE"
```
Expected: the section prints with three numeric values.

- [ ] **Step 3: Commit**

```bash
git add files/live-probe.ps1
git commit -m "live-probe: add CPU delivered-performance (throttle) readout

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Deploy to SysLogs and validate end-to-end

**Files:**
- Modify (deploy copies): `C:\Users\dreux\Documents\SysLogs\scripts\collect-stats.ps1`, `...\live-probe.ps1`

**Interfaces:**
- Consumes: the finished repo `files/*.ps1` from Tasks 2–3.

- [ ] **Step 1: Deploy the two scripts to the running location**

```bash
cp files/collect-stats.ps1 files/live-probe.ps1 /mnt/c/Users/dreux/Documents/SysLogs/scripts/
```

- [ ] **Step 2: Run the deployed hourly collector and confirm a real v3 snapshot**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\dreux\Documents\SysLogs\scripts\collect-stats.ps1"
ls -t /mnt/c/Users/dreux/Documents/SysLogs/hourly/*.json | head -1 | xargs cat | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['schema_version']==3; assert set(d['cpu_perf'])=={'proc_performance_pct_avg','proc_performance_pct_max','frequency_mhz'}; print('OK v3 snapshot:', d['cpu_perf'])"
```
Expected: `OK v3 snapshot: {...}` with numeric values.

- [ ] **Step 3: Confirm the C# parser reads the real deployed snapshot**

This reuses the Task 1 tests (already validate parsing). No new test; the real-file check in Step 2 confirms the emitted shape matches the parser's expected field names. If field names drift, Task 1 tests would not have used the same names — cross-check that the JSON keys in Step 2 output exactly match `proc_performance_pct_avg` / `proc_performance_pct_max` / `frequency_mhz`.

- [ ] **Step 4: No commit needed** (deployment copies live outside the repo). Confirm the branch is clean:

```bash
git status --short
```
Expected: no uncommitted repo changes.

---

## Self-Review

**Spec coverage:**
- Hourly snapshot `cpu_perf` + schema v3 → Task 2 ✓
- Live probe readout → Task 3 ✓
- C# nullable/back-compat parsing + v3 test fixture + kept v2 test → Task 1 ✓
- Deployment (repo `files/` → SysLogs) → Task 4 ✓
- Idle-vs-throttle handled via avg **and max** sampling → Tasks 2 & 3 ✓
- Out-of-scope items (reason/VRM/watts) not implemented ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code; every run step shows the command and expected output.

**Type consistency:** Field names `proc_performance_pct_avg` / `proc_performance_pct_max` / `frequency_mhz` and C# members `CpuProcPerfPctAvg` / `CpuProcPerfPctMax` / `CpuFrequencyMhz` are identical across Tasks 1–4. Counter paths identical across Tasks 2–3.
