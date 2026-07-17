# Root cause: ThrottleStop LOCKS the MSR power-limit register at the Battery profile's values

**Date:** 2026-07-16 (episode ~21:26 EDT, analysis ~21:50–22:20)
**Status:** CONFIRMED via ThrottleStop TPL window.
**Evidence:** `hwinfo_log_07162026.CSV` (21:25:05 → 21:35:14, 304 rows @ ~2s), `ThrottleStop.ini`, live load test, TPL screenshots (Performance + Battery).

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

The lock is the bug. Two candidate remedies, in order of preference:

1. **Uncheck the `Lock` checkbox** in TPL (MSR Power Limit Controls, and the MMIO Lock
   in the Turbo Power Limits panel) — for the **Battery** profile at minimum, ideally
   for Performance too. Then Save, and **restart** to clear the currently-set lock.
   Profile switches will then actually take effect.
   *Trade-off:* the Lock was presumably enabled to stop Lenovo's DPTF/EC from overriding
   limits. Unchecking it gives that ability back to the platform. The current behaviour
   is strictly worse, but this is the user's tuning call.
2. **Alternative — set `NoSetPL` bit 3** (`0x6` → `0xE`) so the Battery profile never
   writes power limits at all. Battery would then only change EPP (`EnPerfPref3=220`),
   which already produces the 10–11 W efficiency behaviour on its own. This preserves the
   "lock Lenovo out" intent while removing the 17 W write that gets latched. Depends on
   ThrottleStop applying+locking Performance at boot — **verify before relying on it**.

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

## Do not

- Hand-edit `ThrottleStop.ini` while ThrottleStop is running — `SaveOnExit=2` means it
  overwrites the file on exit. Change via the TPL GUI, or edit with ThrottleStop closed.
