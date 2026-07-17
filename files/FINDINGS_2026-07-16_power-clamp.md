# Root cause: ThrottleStop LOCKS the MSR power-limit register at the Battery profile's values

**Date:** 2026-07-16 (episode ~21:26 EDT, analysis ~21:50–22:30; **fix verified ~22:52–23:03**)
**Status:** Root cause CONFIRMED. **Fix APPLIED and VERIFIED** — the unplug/replug cycle no longer latches the clamp. See "Fix verified" below.
**New binding constraint:** heat. With the clamp gone the CPU boosts freely, and with the undervolt firmware-locked it hits 100 °C on transient boosts at light load. See "Third finding: thermal spikes".
**Evidence:** `hwinfo_log_07162026.CSV` (21:25:05 → 21:35:14, 304 rows @ ~2s), `hwinfo_log_07162026_v2.CSV` (22:56:16 → 22:59:34, 101 rows @ ~2s, spans the replug), `throttlestop-2026-07-16.txt` (22:52:39 → 23:02:59, 621 rows @ 1s), `unplug_plug.docx` (3 TPL/TS screenshots + timeline), `ThrottleStop.ini`, live load test, FIVR screenshot, BIOS/microcode query.

## Machine

| | |
|---|---|
| Model | Lenovo **Legion 7 16IRX9** (83FD), i9-14900HX, 330 W charger |
| CPU | 24C/32T — 8 P-cores + 16 E-cores. Base **2200 MHz** (`NonTurboRatio=0x15`=21→2.1–2.2 GHz), turbo ~5.8 GHz, spec max turbo power ~157 W |
| BIOS | **NSCN41WW**, released 2026-01-04 |
| Microcode | **0x133** (well past Intel's Raptor Lake Vmin-shift mitigations 0x125→0x129→0x12B) |
| Windows power plan | Balanced (`381b4222-…`) |
| Tools running | ThrottleStop 9.7, Lenovo Legion Toolkit, MSI Afterburner (all deliberate — not bloat) |
| `ThrottleStop.ini` | `/mnt/c/Users/dreux/Desktop/Utilities/ThrottleStop.ini` |
| Secure Boot | blocks LHM/direct MSR reads — use `% Processor Performance` perf counter instead |

## Finding (CONFIRMED)

ThrottleStop's **Lock** setting locks MSR 0x610 at whatever power limits were last
written *before* the lock was set. The **first unplug of the day** causes the Battery
profile to write **17/20/12** and set the lock. From that moment the register is
read-only until CPU reset — so the Performance profile's **70/90/14** write is
**silently rejected**, and the machine stays at 17 W all day regardless of which
profile is selected. A restart clears it.

### The decisive proof — TPL window

The `Turbo Power Limits` panel shows the *actual register*. In **both** the Performance
and Battery TPL screenshots it reads, with the padlock lit and Lock checked:

```
MSR    PL1 = 17    PL2 = 20    Time = 12    [locked]
MMIO   PL1 = -     PL2 = -     Time = -     [Lock checked]
```

| Profile (what it asks for) | PL1 | PL2 | **Turbo Time Limit** |
|---|---|---|---|
| Performance | 70 | 90 | **14** |
| Battery | 17 | 20 | **12** |
| **Actual MSR register** | **17** | **20** | **12** ← matches Battery |

**The Turbo Time Limit of 12 is the clincher** — a third independent value matching
Battery, not Performance, and not any plausible Lenovo/DPTF default. The register holds
the Battery profile's exact three-value fingerprint. This rules out the competing
hypothesis (H2: platform/EC/DPTF enforcing 17 W via MMIO underneath ThrottleStop) — MMIO
reads `-`, and no external agent would coincidentally land on 17/20/**12**.

**Why the profile *looks* active but does nothing:** EPP is a different register
(IA32_HWP_REQUEST) and is **not** locked. So selecting Performance genuinely applies
`EnPerfPref0=32` (visible in the main window) while its power-limit write is rejected.
Selecting Battery applies `EnPerfPref3=220`, dropping *demand* to 10–11 W — below the
17 W cap, so PL1 stops flashing. The cap never moves; only demand does.

### Sequence (matches the user's lived experience exactly)

1. **Boot** → register unlocked → Performance applies 70 W → fast morning.
2. **First unplug** → Battery profile writes 17/20/12 **and sets the lock**.
3. **Replug** → Performance's 70 W write silently rejected → **still 17 W**.
4. Stuck at 17 W regardless of profile until a restart. User reports: fast in the
   morning, degraded "later in the day after it's been unplugged/plugged a few times,"
   restored by the end-of-day restart. Not gradual — the *first* unplug does it.

## Original observation (how we got here)

The CPU is clamped to **PL1 = 17 W / PL2 = 20 W** while plugged in and charging at 65 W.

### HWiNFO log (the 9:26 PM episode)

| Signal | Value |
|---|---|
| `PL1 Power Limit (Static)` | **17.0 W** — constant, all 304 rows (no other value appears) |
| `PL2 Power Limit (Static)` | **20.0 W** — constant, all 304 rows |
| `CPU Package Power` | min 15.6 / mean 17.3 / max 19.2 W — pinned at the 17 W ceiling |
| `IA: Package-Level RAPL/PBM PL1` | **Yes in 304/304 rows** (continuously PL1-limited) |
| `IA: PROCHOT` | Yes in **2/304** rows (~0.7%) — effectively absent |
| `Core Temperatures (avg)` | 49–58 °C — cool |
| `Charge Rate` | ~65 W, charge 59.1% → 71.0% — **on AC the whole time** |

### ThrottleStop.ini — decoded MSR 0x610 (power unit 0.125 W)

| Profile | PL1 | PL2 |
|---|---|---|
| 0 Performance | 70.0 W | 90.0 W |
| 1 Game | 105.0 W | 162.0 W |
| 2 Internet | 105.0 W | 162.0 W |
| **3 Battery** | **17.0 W** | **20.0 W** ← exact match to the log |

Relevant keys: `BatteryMonitoring=1` (auto AC/DC profile switching), `NoSetPL=0x6`
(profiles 1,2 skip power limits — profile 3 **does** set them), `EnPerfPref3=220`
(Battery profile EPP heavily efficiency-biased vs 32 for Performance),
`LockPowerLimits=1`, `MSRLock=0x9` / `SyncMMIO=0x9` (bitmask = profiles 0 and 3).

### Live repro (21:50, plugged in, hours after the episode)

Sustained 100% all-core load, driver-free `% Processor Performance` (base 2200 MHz):

```
CPU utilization        : 100 %
% Proc Performance     : 62, 62, 65, 63, 63, 63, 63, 63   (avg 63, max 65)
```

**63% of base ≈ 1.4 GHz all-core.** The clamp is still active and is not transient.
(At idle it reads ~90–98% — an idle CPU never demands >17 W, so the clamp only
shows under load. This is why light use feels fine and the Start menu stutters.)

## Why "plugged in but still slow"

Replugging fixed the *EC* side (blue → red / Performance) but the MSR was already
**locked** at 17 W — which is precisely why responsiveness "improved a bit, but still
not perfect." The EC lifted its limits; the locked register did not.

The **blue light on a 330 W brick** is a separate AC-detection fault. It is the likely
*trigger* (it makes ThrottleStop's `BatteryMonitoring=1` see DC and apply the Battery
profile), but it is **not** the mechanism that holds 17 W — the lock is. Note the log
shows the battery charging at 65 W (charging circuit knew it was on AC) while still
capped, so the cap outlives the fault. Worth fixing separately, but it is secondary.

## Ruled out

- **Thermal / thermal paste** — 49–58 °C under load. The CPU is *power-starved, not
  heat-limited*. A repaste would not affect this. Do not repaste for this problem.
- **BD PROCHOT** — asserted in only 2/304 rows this episode. See reconciliation below.
- **Load / RAM / adapter** — on AC at 65 W charge, cool, low utilization.

## Reconciliation with the 2026-07-15 BD PROCHOT diagnosis

Not "the old diagnosis was wrong" — it was looking at a co-symptom. Last session also
recorded **PL1 red**, so PL1 was clamping then too. The 17 W / ~1.6 GHz figure common to
both sessions is fully explained by the locked Battery-profile PL1. BD PROCHOT was
incidental (2/304 rows on 2026-07-16 while fully clamped) and is **not** the mechanism.

## The fix

The lock is the bug. **Uncheck the `Lock` checkbox on every profile** in TPL (MSR Power
Limit Controls, plus the MMIO Lock in the Turbo Power Limits panel), Save, then
**restart** to clear the currently-set lock.

With no lock, nothing latches: PL simply follows the active profile — **17 W on battery**
(the deliberate battery profile, where that value belongs) and **70 W on AC**. That is
the intended behaviour. The entire bug was 17 W leaking onto AC *through the lock*.

**Do not keep the Lock on Performance** on the theory that it would latch 70/90/14 at
boot and protect against the Battery write. The evidence refutes this: if Performance's
lock engaged at boot, the first unplug's Battery write would be *rejected* and the
machine would stay fast all day. It doesn't — it clamps to 17 W. So Performance's lock
is not firing at boot, and on replug it cannot fire either because Battery has already
locked the register. Keeping it buys nothing and leaves a second way for a bad value to
latch.

*Trade-off:* the Lock presumably existed to stop Lenovo's DPTF/EC from overriding limits;
unchecking hands that back to the platform. Legion Toolkit manages modes anyway. If DPTF
turns out to lower AC limits post-unlock, that is a fresh, observable problem to handle
then — not a reason to keep the latch.

**Rejected alternative:** setting `NoSetPL` bit 3 (`0x6` → `0xE`) so Battery never writes
power limits. This leans on the same murky boot/profile-switch ordering that the evidence
above shows is not understood — it is *more* fragile than unchecking Lock, not cleaner.

**Verification after any change:** reboot → confirm TPL `MSR` row reads **70/90/14** →
unplug, replug → re-check TPL still reads 70/90/14 → run an all-core load and confirm
`% Processor Performance` goes **>100** (not ~63).

Raising the Battery profile's PL1 above 17 W is *not* a fix — it only changes which
value gets latched.

---

## FIX VERIFIED (2026-07-16, 22:52–23:03)

Lock unchecked on all profiles + restart. The user then ran a deliberate unplug/replug
test with HWiNFO and ThrottleStop logging. **The clamp no longer latches.** Six
independent lines of evidence:

| # | Evidence | Before (clamped) | After (fixed) |
|---|---|---|---|
| 1 | TPL `MSR` row (screenshot, on AC) | 17 / 20 / 12, padlock lit | **70 / 90 / 14, Lock unchecked** |
| 2 | PL1 dynamic at replug (22:57:14→:16) | stayed 17 | **17 → 70** |
| 3 | PL2 dynamic at replug | stayed 20 | **20 → 90** |
| 4 | CPU Package Power on AC | pinned 15.6–19.2 W | **33–92.7 W** (mean 41) |
| 5 | Core clocks on AC | ~1.4 GHz | **2.6–4.3 GHz avg, peak 5.4 GHz** |
| 6 | `IA: Package-Level RAPL/PBM PL1` | **Yes 304/304 (100%)** | **Yes 12/101 (11.9%)** |

The decisive moment is in `hwinfo_log_07162026_v2.CSV`: at **22:57:14** `Charge Rate`
goes −44 W → **0.000** (AC detected) and at **22:57:16** PL1 dynamic reads **70** and
PL2 **90**, with package power jumping 12 W → 33 W and climbing. The third screenshot,
taken ~20 s after the replug, shows the Performance profile active at **39.8 W /
4392 MHz**. That is the exact move that used to leave the machine at 17 W / 1.4 GHz.

### Trap: HWiNFO `PL1 Power Limit (Static)` still reads 17 W — ignore it

It reads **17.0 W in all 101 rows**, including rows where the package is drawing
**92.7 W**. That is physically impossible under a real 17 W PL1, so the field is stale —
almost certainly cached when logging started (which was **while unplugged**, when 17 W
was genuinely correct for the Battery profile). **Use `PL1 Power Limit (Dynamic)`**,
which tracks the live enforced limit (sits at 70 = Performance's PL1, excursions to 90 =
PL2, and DPTF wander 50–85). The 2026-07-15 log's Static=17 was correct only by
coincidence — it agreed with a clamp that was genuinely present.

---

## Third finding: thermal spikes are now the binding constraint

With the clamp gone the CPU boosts freely — and **heat, not power, is now what limits
the machine.** From `throttlestop-2026-07-16.txt` (621 samples @ 1 s):

| Signal | Value |
|---|---|
| `TEMP` | mean 76 °C, **max 100 °C**; ≥95 °C in **63/621** (10%), ≥99 °C in **16/621** (2.6%) |
| `VID` | mean 1.21 V, **max 1.50 V**; **>1.3 V in 385/621 (62%)** |
| `C0%` (load) | **mean 10.4%** — the machine is essentially idle |
| `POWER` | mean 37 W, max 108 W |
| `MULTI` | max **53.87** (5.39 GHz) |
| HWiNFO `Core Thermal Throttling` | **Yes in 41/101 (40.6%)** |

The user's report — "PROCHOT has hit multiple times and I'm not doing anything, just
opened Steam, Xbox app, VS Code" — is **confirmed by the data**.

### Mechanism: single-core boost spikes at stock voltage

Every 100 °C event is the same shape — **high multiplier, low load**:

```
23:02:14  multi=48.94  C0%=7.9   temp=100  vid=1.3710  pwr=40.9
23:02:49  multi=50.05  C0%=7.2   temp=100  vid=1.3289  pwr=44.2
23:01:31  multi=50.51  C0%=9.4   temp=100  vid=1.3882  pwr=55.5
```

**100 °C at 41 W and 7% load is not a cooling failure.** One P-core at ~5 GHz / 1.37 V
has enormous *local* power density; package temperature reports the **hottest core**,
while package *power* is a die-wide average. The core spikes faster than heat can even
reach the heatsink — no fan can win that race. The chip is boosting to **5.0–5.4 GHz to
launch Steam**, and because the undervolt is **firmware-locked** (see second finding),
every one of those spikes runs at full stock voltage.

**Failure mode is voltage/boost, not heat transfer.**

### The fans work — but they lag ~90 s

From `hwinfo_log_07162026_v2.CSV`:

| Time | CPU fan | pkg °C | pkg W |
|---|---|---|---|
| 22:57:10 (on battery) | 1400 | 60 | 12 |
| 22:57:16 (replug +2 s) | **1400** | 67 | 33 |
| 22:57:28 | 2000 | 88 | 43 |
| 22:58:22 | 2400 | 88 | 79 |
| 22:58:46 | **4200** | **100** | 86 |
| 22:59:04 (fans maxed) | 4200 | **85** | 71 |

Fans idle at **1400 RPM** and take **~90 s** to reach 4200. Boost arrives in
milliseconds. The 100 °C hits land *during the ramp*. **Once fans are at 4200 RPM,
sustained 71 W settles at 85–89 °C** — which is normal for a 14900HX and is positive
evidence the cooling path is healthy.

### This *re-confirms* "do not repaste"

The 2026-07-15/16 "do not repaste" call was made on power-starved data (49–58 °C at
17 W) and could reasonably have been doubted once real load appeared. **It survives.**
The steady-state figure (71 W → 86 °C at full fans) is what a healthy Legion 7 cooler
does with this chip. The spikes are transient single-core voltage events, which a
repaste cannot fix. **Still do not repaste.**

## Instrumentation validation (was pending from 2026-07-15)

**PASSED.** The `cpu_perf` signal predicted ~66% for a clamped CPU; the live clamp
measured **63%** under load. The driver-free `% Processor Performance` counter does
detect the clamp. Caveat learned: **it only reads low under load** — at idle it reads
~90–98% even while clamped, so the probe must apply load (or be interpreted alongside
utilization) to be meaningful.

---

## Second finding: undervolting is LOCKED by firmware

The ThrottleStop **Turbo FIVR Control** window shows the plane group box titled
**"Locked"** (CPU Core / CPU P Cache / System Agent / Intel GPU / iGPU Unslice /
CPU E Cache), `Unlock Adjustable Voltage` **checked but greyed out**, and Turbo
Overclocking showing a padlock. ThrottleStop still *displays* the stored −89.8 mV
offset but almost certainly **cannot write it**.

**Cause:** BIOS NSCN41WW + microcode **0x133** — well past Intel's Raptor Lake
Vmin-shift mitigation series. OEMs (Lenovo included) disabled the overclocking/
undervolting mailbox as part of those mitigations. The user independently suspected a
BIOS update did this; they were right. Widespread, not a misconfiguration.

**NEVER roll back the BIOS (or use a BIOS unlock mod) to regain undervolting.** The
i9-14900HX is an affected Raptor Lake SKU. The Vmin-shift degradation is real,
cumulative and **permanent**. Do not trade protection against irreversible CPU damage
for an undervolt.

**Consequence — this reframes the machine's history.** The user fixed out-of-the-box
PROCHOT with *two* changes: an undervolt **and** a 70 W PL1 cap. The undervolt has since
gone inert after a BIOS update, leaving the 70 W cap carrying the load alone — which fits
their reported "mixed success."

**UPDATE 2026-07-16 — the 70 W cap is NOT sufficient to prevent PROCHOT.** Once the MSR
lock was removed and PL1 = 70 actually applied, the machine hit **100 °C anyway**, in
16/621 samples at ~10% load. So the surviving half of the original fix does not hold the
line by itself. Confirmed: the undervolt was doing the real work, and losing it to
firmware is *why* PROCHOT is back.

**Voltage is no longer a *direct* lever — but it is still the right target.** PL1 is a
poor substitute: the 100 °C events occur at **41–55 W**, far under a 70 W PL1, so PL1
never engages. Reduce voltage *indirectly* by capping peak multiplier (V(f) is
superlinear at the top) and softening EPP. See TODO 2b.

---

## ThrottleStop settings audit (2026-07-16)

Profile names: 0=Performance, 1=Game, 2=Internet, 3=Battery. `Profile=0`.

### Undervolt (FIVR offsets, mV) — currently NOT applying (locked, see above)

| Plane | Performance | Game | Internet | Battery |
|---|---|---|---|---|
| CPU Core | −89.8 | — | — | −90.8 |
| CPU Cache/Ring | −89.8 | — | — | −90.8 |
| (plane 5) | −89.8 | — | — | −45.9 |
| iGPU / System Agent / iGPU Unslice | — | — | — | — |

Game and Internet have **no undervolt at all**.

### Power limits

| Profile | PL1 | PL2 | PL1 clamp | PL2 clamp | Applied? |
|---|---|---|---|---|---|
| Performance | 70 W | 90 W | yes | no | **yes** |
| Game | 105 W | 162 W | yes | no | **NO** — `NoSetPL` bit 1 |
| Internet | 105 W | 162 W | yes | no | **NO** — `NoSetPL` bit 2 |
| Battery | 17 W | 20 W | yes | no | **yes** |

`NoSetPL=0x6` → Game/Internet **never write power limits**; they defer to the Lenovo EC
(which in Performance/red mode allows far more than 70 W). Their 105/162 values are dead
config. The GUI equivalent is *Disable Controls* in the TPL window. Note this means
**"Performance" is the user's most-capped AC profile** — TS holds it to 70 W while Game
caps nothing.

**PL1 clamp = 1 on all profiles** — permits dropping below base clock to hold PL1. This
is why 17 W produced ~1.4 GHz (63% of the 2200 MHz base) rather than a base-clock floor.

### Other

| Setting | Performance | Game | Internet | Battery |
|---|---|---|---|---|
| EPP (`EnPerfPref`, 0=max perf, 255=max efficiency) | **32** | 128 | 128 | **220** |
| PROCHOT offset (below Tjmax=100) | 1 → **99 °C** | 3 → 97 °C | 3 → 97 °C | 3 → 97 °C |

`PowerLimit4=0x7B0` → PL4 = **246 W** (peak/current limit, effectively unrestricted).
`BatteryMonitoring=1` (auto AC/DC switching). `LockPowerLimits=1`,
`MSRLock=0x9`/`SyncMMIO=0x9` (= profiles 0 and 3 — **the bug**).

### Options window (2026-07-16)

**Default Profiles** (1-based): ☑ AC Profile = **1** (Performance), ☑ Battery Profile =
**4** (Battery), Low Battery % = 0 (disabled), Low Battery Profile = 4. This is the
auto-switching the user relies on — "never have to touch ThrottleStop."

**Miscellaneous:** ☑ Battery Monitoring, ☑ Start Minimized, ☑ Minimize on Close, ☑ Click
on MHz to Minimize, ☑ Nvidia GPU, ☑ Grid Lines, ☑ **Windows Defender Boost** (`WDBoost=1`
— already on; relevant to the separate MsMpEng CPU-spike issue), ☐ Disable Safe Start,
☐ DC Exit Time (0), Timer Resolution 1.00 ms, AC Timer Res 16, PowerSaver C0% 35
(`PSMinimum=35`), Force TDP/TDC greyed. Alarm off (DTS 1 / GPU 105 °C).

**Run Program After Profile Change: Disabled** (`BeforeRunProgram=15`). Note this is the
*inverse* of app-triggered switching — see TODO 4.

**HotKeys:** `HotKey0..4=0x0` — all unset. Log folder `C:\Users\dreux\Desktop\Logs`.

### Where 70 W came from

**Not a deliberate thermal choice.** The user followed tuning guides a while back to stop
PROCHOT, with "mixed success," and does not recall the origin of 70 W. Treat it as tunable
— but see the FIVR-lock caveat above before raising it.

---

## Next session — TODO

### 1. ~~Verify the fix~~ — **DONE 2026-07-16. Fix verified.** See "FIX VERIFIED" above

Unchecking `Lock` on all profiles + restart resolved the clamp. The unplug/replug cycle
now correctly restores 70/90 on AC. **This issue is closed.** The only step not
re-executed was the all-core `% Processor Performance` load test — unnecessary, since
package power (33–92.7 W) and clocks (up to 5.4 GHz) directly demonstrate the clamp is
gone, which is what that proxy existed to infer.

Load-test command retained for future PL work (WSL → PowerShell, native threads; the
PowerShell `Start-Job` version is too weak — it only reached 19% utilization and proves
nothing):

```powershell
Add-Type -TypeDefinition @'
using System; using System.Threading;
public class Load {
  public static volatile bool Stop = false;
  public static void Burn(int n) {
    for (int i=0;i<n;i++) { var t = new Thread(() => { double x=0; while(!Stop) { x += Math.Sqrt(x+1.234); if (x>1e300) x=0; } }); t.IsBackground=true; t.Start(); }
  }
}
'@
[Load]::Stop = $false
[Load]::Burn([Environment]::ProcessorCount)
Start-Sleep -Seconds 6   # let PL1 tau settle
(Get-Counter '\Processor Information(_Total)\% Processor Performance' -SampleInterval 1 -MaxSamples 8).CounterSamples | ForEach-Object { [math]::Round($_.CookedValue,0) }
[Load]::Stop = $true
```

### 2. ~~Walk PL1 up 70 → 90 → 110~~ — **CONTRAINDICATED by the 22:52–23:03 data**

**Do not raise PL1.** That plan was written before we had unclamped thermal data. It
assumed the machine had thermal headroom to spend. It does not: it already hits 100 °C
and thermal-throttles in **40% of samples at ~10% CPU load**. Raising PL1 spends
headroom that isn't there and makes the spikes worse. **Heat is the binding constraint
now, not power** — the goal has inverted from "let it draw more" to "stop it boosting so
hard for trivial work."

Also retire the premise that "70 W is what prevents PROCHOT" — at PL1 = 70 the machine
*does* hit PROCHOT. PL1 is the wrong knob: it caps sustained die-average watts, but the
100 °C events are **single-core voltage spikes at 41–55 W**, which sail under a 70 W PL1
untouched.

### 2b. Instead — attack boost voltage. Tune live, one variable at a time

With FIVR undervolting firmware-locked, voltage can only be reduced *indirectly*, by not
asking for the top bins. V(f) is superlinear at the top of the curve, so shaving peak
multiplier sheds voltage fast for little real-world loss. Levers, strongest first:

1. **Max multiplier cap** (`Set Multiplier`, currently unchecked; peak seen **53.87**).
   Capping ~54 → ~48–50 targets exactly the 1.37–1.50 V spikes. Best
   temperature-per-lost-performance on this machine.
2. **EPP on Performance** (currently **32** = very aggressive). Raising to ~64–84 makes
   the governor less eager to jump to 5.4 GHz to open Steam. Directly targets
   "max boost for background work."
3. **Fan floor / curve** (Legion Toolkit). Fans idle at **1400 RPM** and take ~90 s to
   reach 4200. A higher floor won't stop a millisecond spike, but it removes the ~90 s
   window where every spike lands on a cold fan. **Depends on the power-mode question
   below — resolve that first.**

Do this live, with temps on screen — not a blind edit. PROCHOT fires at 99 °C on
Performance.

### 2c. ~~OPEN QUESTION: which Legion power mode?~~ — **ANSWERED: Performance/red**

User-confirmed sequence: **red (Performance) → unplug → blue (Quiet) → HWiNFO logging
started here → replug → red (Performance)**, log still running. So the entire post-replug
load test (22:57:14 onward) ran in **Performance/red**.

**This closes the fan lever.** The hypothesis was that a conservative *Balanced* curve was
losing the race and that switching to Performance would blunt the spikes cheaply. Wrong —
it was **already in Performance**, i.e. Lenovo's most aggressive built-in curve. There is
no better mode to switch to.

**And the curve is behaving correctly, not lazily.** Re-reading the ramp against load: the
1400 RPM floor seen at 22:56:16–22:57:12 was the **Quiet/battery** floor (correct — the
machine was unplugged). After the switch to red at 22:57:14, fans tracked *sustained*
heat: the genuinely sustained load only began ~22:58:22 (40% usage, 79 W), and fans hit
**4200 RPM by 22:58:40 — ~20 s later.** That is a sane response. The curve deliberately
ignores sub-second transients, which is correct design (you do not want fans screaming
every time a menu opens).

**Conclusion: fans are working as designed and are NOT the lever.** No fan can win against
a single-core spike anyway — core→heatsink thermal mass is the limit, not airflow. A
custom LLT curve with a higher floor is available but would buy little; **do not spend
effort here.** Go straight to the multiplier cap (2b lever 1) and EPP (lever 2). Demote
lever 3 accordingly.

**Note this is a different axis from the blue-light AC fault** — ThrottleStop's *profile*
switching (PL1 → 70, discharge → 0 W) is independent of the Legion EC's *fan/power mode*.
Both worked correctly this run.

### 2d. Minor, flagged not chased: `Charge Rate` = 0.00 W at 77% on AC

Post-replug the battery sits at 77% drawing **0 W** — it should be charging. Possibly
Lenovo conservation mode or a charge threshold in Legion Toolkit. The prior log charged
at 65 W. Not performance-relevant; note only.

### 3. Then build the Gaming profile

User's intent: **Performance** = plugged-in daily driver (auto on AC), **Gaming** =
manual flip for gaming sessions only, **Battery** = auto on DC. They have **never used**
the Game or Internet profiles — that is why those profiles' settings look arbitrary.

**The Game profile as it stands is a PROCHOT trap:** no power limit (defers to the EC's
full power) + no undervolt = max power with zero voltage headroom, precisely the
out-of-the-box condition that caused their original PROCHOT problem. Give Gaming an
**explicit, thermally-validated PL** from step 2 — do not let it run uncapped.

### 4. Auto-switch to Gaming on game launch — ThrottleStop CANNOT do this natively

**Settled 2026-07-16 from the Options window.** ThrottleStop's only automatic profile
switching is by power source:

| Options → Default Profiles | Value | Meaning (**1-based** numbering) |
|---|---|---|
| ☑ AC Profile | **1** | on AC → **Performance** |
| ☑ Battery Profile | **4** | on DC → **Battery** |
| Low Battery % | 0 | disabled |
| Low Battery Profile | 4 | Battery |

There is **no app/process-triggered switching option**. "Run Program After Profile
Change" is the *inverse* feature (run a program *when the profile changes*) and is
**Disabled** — that explains the `BeforeRunProgram=15` ini key; it is not what we want.

So a Gaming profile must be flipped **manually**, or automated externally — Process Lasso,
or a small process-watcher script that switches ThrottleStop's profile on game launch.
Worth checking whether ThrottleStop exposes a command-line/hotkey for profile switching
(`HotKey0..4=0x0` are all unset, so hotkeys are available and are the cheapest option).

**Caveat for a manual Gaming flip:** since AC Profile is pinned to 1 (Performance), any
AC/DC transition *during* a gaming session — including a spurious one from the blue-light
AC-detection fault — will knock the profile back to Performance, not Gaming.

### 5. Secondary: the blue-light AC-detection fault — **did NOT reproduce on 2026-07-16**

Plugging into the 330 W brick *sometimes* leaves the Legion in Quiet/blue instead of
Performance/red — the machine does not register AC. This is the **trigger** that makes
`BatteryMonitoring=1` apply the Battery profile. Now that the lock is gone it should be
**harmless** (a glitch would apply 17 W, and replugging restores 70 W instead of
latching). Worth fixing separately via BIOS/Legion Toolkit if it keeps happening.

**2026-07-16 test: AC detection worked correctly.** User-confirmed red → unplug → blue →
replug → **red**, and the telemetry agrees (`Charge Rate` −44 W → 0.00 W at 22:57:14,
PL1 17 → 70 at :16). The fault is intermittent ("sometimes"), and it did not fire here.

**Do not mistake normal behaviour for the fault.** **Blue/Quiet while unplugged is
correct and by design** — the Legion drops to Quiet on DC. The fault is *specifically*
**staying blue after replugging**. Only that counts as a reproduction.

## Do not

- **Do not repaste the CPU.** Ruled out twice, on two independent datasets. Originally on
  power-starved data (49–58 °C at 17 W). **Re-confirmed 2026-07-16 on unclamped data:**
  sustained 71 W settles at 85–89 °C with fans at 4200 RPM — normal for a 14900HX and
  positive evidence the cooling path is healthy. The 100 °C events are transient
  single-core voltage spikes (100 °C at **41 W / 7% load**), which no repaste can fix.
- **Do not roll back the BIOS** to regain undervolting (see above).
- **Do not hand-edit `ThrottleStop.ini` while ThrottleStop is running** — `SaveOnExit=2`
  overwrites the file on exit. Use the TPL GUI, or edit with ThrottleStop closed.
- **Do not re-investigate BD PROCHOT or thermals** as the cause of the chronic slowness.
- **Do not use the PowerShell `Start-Job` load test** — too weak to make PL1 bite.
