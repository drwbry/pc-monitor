# Root cause: ThrottleStop LOCKS the MSR power-limit register at the Battery profile's values

**Date:** 2026-07-16 (episode ~21:26 EDT, analysis ~21:50–22:30)
**Status:** Root cause CONFIRMED via ThrottleStop TPL window. **Fix identified but NOT yet applied or verified** — see "Next session" at the bottom.
**Evidence:** `hwinfo_log_07162026.CSV` (21:25:05 → 21:35:14, 304 rows @ ~2s), `ThrottleStop.ini`, live load test, TPL screenshots (Performance + Battery), FIVR screenshot, BIOS/microcode query.

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
PROCHOT with *two* changes: an undervolt **and** a 70 W PL1 cap. If the undervolt went
inert after a BIOS update, **the 70 W cap is what has actually been preventing PROCHOT**
— which fits their reported "mixed success." That matters because 70 W is also what caps
their performance: the fix and the limitation may be the same knob.

**Voltage is no longer a lever on this machine — PL1 is the only one left.** Any PL1
increase must be validated empirically against temps, with no undervolt headroom.

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

### 1. Verify the fix (do this first)

The user was to uncheck **`Lock` on all four profiles** in TPL (MSR Power Limit Controls
+ the MMIO Lock in the Turbo Power Limits panel), Save, and restart. **Outcome unknown as
of this writing.** Verify:

1. TPL `MSR` row reads **70 / 90 / 14** (not 17/20/12).
2. **Unplug, replug** → still 70/90/14. *This is the real test — it is the exact move
   that has been breaking it.*
3. All-core load → `% Processor Performance` **>100** (not ~63).

Load-test command that produced the 63% repro (WSL → PowerShell, native threads; the
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

### 2. Then tune PL1 — as a SEPARATE change, one variable at a time

User wants **max performance on the 330 W charger**. 70 W is conservative for a chip
rated ~157 W. But there is **no undervolt** to fall back on, and the 70 W cap may be the
only thing that has been preventing PROCHOT. So: walk PL1 up (70 → 90 → 110 → …) under
sustained load with **temps on screen**, stop where the user is comfortable. Do this
live together — not a blind edit. PROCHOT fires at 99 °C on the Performance profile.

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

### 5. Secondary, unfixed: the blue-light AC-detection fault

Plugging into the 330 W brick sometimes leaves the Legion in Quiet/blue instead of
Performance/red — the machine does not register AC. This is the **trigger** that makes
`BatteryMonitoring=1` apply the Battery profile. Once the lock is gone it should be
**harmless** (a glitch would apply 17 W, and replugging restores 70 W instead of
latching). Worth fixing separately via BIOS/Legion Toolkit if it keeps happening.

## Do not

- **Do not repaste the CPU.** 49–58 °C under load, 73 °C max. Power-starved, not
  heat-limited. This was explicitly considered and ruled out.
- **Do not roll back the BIOS** to regain undervolting (see above).
- **Do not hand-edit `ThrottleStop.ini` while ThrottleStop is running** — `SaveOnExit=2`
  overwrites the file on exit. Use the TPL GUI, or edit with ThrottleStop closed.
- **Do not re-investigate BD PROCHOT or thermals** as the cause of the chronic slowness.
- **Do not use the PowerShell `Start-Job` load test** — too weak to make PL1 bite.
