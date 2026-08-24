# Retune plan: FIVR unlocked via BIOS — what to do in ThrottleStop

**Date:** 2026-08-23
**Trigger:** User enabled the Legion performance/overclocking setting in BIOS and reports
undervolting is now possible. If true, this is the **new firmware evidence** that
`project-fivr-undervolt-locked` named as the only grounds for reopening.

**Status: REOPENED — and the telemetry below strongly supports the user.** The 2026-08-08 A/B
(6.7 mV gap against a ~90 mV request -> LOCKED) is **superseded**. Two independent markers in
ThrottleStop's own daily logs turn on at the same reboot this morning. Step 1 remains worth
running as confirmation, not as an open question.

---

## What the telemetry actually says

### Correction: HWiNFO has NOT been logging

Last HWiNFO CSVs are `hwinfo_log_07162026.CSV` and `..._v2.CSV`, both **2026-07-16**.
Nothing since. The belief that HWiNFO has been running is wrong.

**ThrottleStop's own logging, however, has been running continuously and daily** to
`C:\Users\dreux\Desktop\Logs\YYYY-MM-DD.txt` — 15k-58k rows/day at 1 Hz, back through July.
For this question that is the *better* instrument: it carries `VID`, which HWiNFO's CSV
did not. All numbers below come from those logs.

Column layout (note `NVIDIA GPU` spans **two** columns — clock and temp):

```
1 DATE  2 TIME  3 MULTI  4 C0%  5 CKMOD  6 BAT_mW  7 TEMP  8 GPUMHz  9 GPUtemp  10 VID  11 POWER  12+ throttle reasons

# GOTCHA: these are CRLF files. The LAST token on each line carries a trailing \r, so a
# reason tally keyed on $i silently splits into "TEMP" and "TEMP\r" as two distinct keys.
# Always: x=$i; sub(/\r$/,"",x)   -- this bit me once already.
```

### The undervolt IS applying. Two independent markers turn on at the same reboot.

Today's log has several session gaps. The one that matters is **11:17:59 → 11:30:01** — a
12-minute gap, i.e. a reboot. (A first pass split at the 21:55 ThrottleStop *app* restart,
then at the 21:10 reboot; both were wrong. The BIOS change lands at ~11:30.)

**Marker 1 — throttle reason codes that have never appeared before.** `TVB` (Thermal
Velocity Boost ratio clipping) first fires at **11:31:21**. Across the **23 prior daily logs
it appears exactly zero times.** `VMAX` jumps similarly (2/11/0/4 on prior days → 111 today).
New reason codes appearing means ThrottleStop can suddenly see and act on turbo/voltage
fields it could not touch before — the signature of the OC mailbox opening.

**Marker 2 — VID drops by almost exactly the requested offset,** at `MULTI >= 46`:

| Arm | n | avg MULTI | VID median | VID p90 |
|---|---|---|---|---|
| 2026-08-13 (locked) | 385 | 49.39 | 1.4150 | 1.4493 |
| 2026-08-20 (locked) | 1051 | 47.80 | 1.4060 | 1.4528 |
| 2026-08-21 (locked) | 300 | 47.87 | 1.3983 | 1.4452 |
| 2026-08-22 (locked) | 122 | 48.21 | 1.4023 | 1.4406 |
| 08-23 **00:00-11:18** (locked) | 287 | 47.99 | 1.4160 | 1.4572 |
| **08-23 11:30-12:12 (post)** | 85 | **49.66** | **1.3083** | **1.3667** |
| 08-23 21:10-22:11 (post) | 91 | 48.79 | 1.3309 | 1.4252 |

Five independent locked arms span just **17.7 mV** on the median (1.3983-1.4160) and
**16.6 mV** on p90 (1.4406-1.4572). The 11:30 arm sits **90-108 mV below** on the median and
**74-90 mV below** on p90 — *while running a higher average multiplier*, which if anything
should have pushed VID up.

The configured offset is **89.84 mV**. The p90 shift against the locked mean is **~90.5 mV**.

Crucially, **the median and the envelope move together here**, which is what a genuine
whole-curve offset looks like. (An earlier pass that split at 21:10 showed the median moving
65 mV while p90 barely moved — that arm was contaminated by 10 hours of post-change data
sitting in its "before" side.)

**Remaining caveats, honestly:** n=85 in the cleanest arm, and the 21:10-22:11 arm is weaker
(p90 1.4252 is close to the locked band). `MULTI` is a 1 s average while `VID` is an
instantaneous single-core read, so passive rows never perfectly match operating points. The
controlled A/B in Step 1 is still worth the two minutes — but it is now a **confirmation**,
not an open question. Plan on the undervolt being live.

### Thermal and throttle baseline (record before changing anything)

| Day | rows | mean °C | ≥95 °C | ≥100 °C | reasons |
|---|---|---|---|---|---|
| 08-20 | 40,814 | 65.3 | 3.10% | 0.89% | TEMP 1157, PL2 118, PL1 48, EDP 46, VMAX 11 |
| 08-21 | 57,923 | 57.6 | 0.44% | 0.12% | **EDP 6204**, TEMP 220, PL1 153, PL2 47 |
| 08-22 | 47,659 | 58.7 | 0.26% | 0.08% | **EDP 5015**, PL1 387, TEMP 104, PL2 44 |
| 08-23 | 15,351 | 69.8 | 3.38% | 0.87% | **TVB 573**, PL1 360, TEMP 370, VMAX 65, PL2 56, EDP 24 |

Two things to note in passing, neither of which should become its own investigation:
- **EDP throttling spiked to ~5–6 k samples/day on 08-21 and 08-22** and is otherwise rare.
  EDP is a current/electrical limit. Undervolting reduces current draw at a given clock, so
  this is a supporting argument *for* undervolting, not a separate thread to pull.
- **TVB appears only today (573 samples).** Thermal Velocity Boost clips ratio above a
  temperature threshold; today also has the highest mean temp of the four days. Consistent,
  not alarming.

---

## Step 0 — Two GUI checks. Ten seconds each. Both gate everything below.

### 0a. Is the mailbox actually open?

Open TS → **FIVR**. On 2026-08-08 the plane group box was titled **"Locked"** and
`Unlock Adjustable Voltage` was checked **but greyed out**.

- Is the "Locked" title gone?
- Is `Unlock Adjustable Voltage` now *clickable* rather than greyed?

While you are in there: **check whether the TVB / Turbo Ratio fields are now populated.**
`TVB` and `VMAX` throttle reasons appear in today's log for the first time in 23 days of
logging, which suggests TS gained access to more than just the voltage offset. `TVBoost=0x7`
and `VMaxStress=0x8` are sitting in the .ini. This is also why the within-profile toggle A/B
(offset only, nothing else moves) is the right test rather than more log slicing.

If yes, you have something you have never had before: a **within-profile A/B**. You can
toggle the offset live under a fixed load with no profile switch, so no EPP or power-limit
difference between arms. That is strictly better than the 2026-08-08 profile A/B.

### 0b. RESOLVED 2026-08-23 from the FIVR screenshot — the mapping is the safe one

The plane order in the FIVR readback table is:

| index | plane | offset on profile 0 |
|---|---|---|
| 0 | **CPU Core** | -89.84 mV |
| 1 | Intel GPU | 0 |
| 2 | **CPU P Cache** | -89.84 mV |
| 3 | iGPU Unslice | 0 |
| 4 | **System Agent** | **0** |
| 5 | **CPU E Cache** | -89.84 mV |

So the .ini's offsets on indices 0/2/5 are **CPU Core + CPU P Cache + CPU E Cache, all
matched, with System Agent untouched.** That is the classic TS index mapping, and it is
exactly the correct and safe configuration. **The System Agent concern is cleared** — no
action needed. Keep Core and P Cache matched whenever you change either.

### 0c. CONFIRMED from the same screenshot — the OC mailbox is open

- **`Turbo Overclocking: Unlimited`**, no padlock. On 2026-08-08 this was padlocked.
- `Unlock Adjustable Voltage` is **checked and no longer greyed**; the plane group box is no
  longer titled "Locked".
- **Turbo Groups are populated and editable** — see Step 3, this is the real prize.
- `uCode 133` confirmed, `PL4 246`, `Cache Ratio 46` (min/max 8/50), `IccMax 511.75 A`.

**Caveat on the readback:** every plane's `Offset` column reads `+0.0000` even though the CPU
Core slider shows -89.8 mV. Do **not** read that either way — on Raptor Lake the FIVR offset
mailbox is frequently write-only, so TS displays 0 regardless of what is applied. The
`Voltage ID` field in that same table (1.2838 at the time of the screenshot) is the live
instrument. Use VID, not the Offset column.

## Step 1 — Prove it applies, before tuning it

Do not skip to picking a value. The whole point of the 2026-08-08 test was that the .ini
stores offsets *as if live* whether or not the CPU accepts them.

**Preferred method (if 0a says the FIVR window is editable) — within-profile A/B.**

Start a fixed single-core load on AC power:

```powershell
$p = Start-Process powershell -PassThru -ArgumentList '-NoProfile','-Command',
  '$e=(Get-Date).AddSeconds(180); $x=1.0; while((Get-Date) -lt $e){ for($i=0;$i -lt 200000;$i++){ $x=[Math]::Sqrt($x+$i) } }'
$p.PriorityClass = 'High'          # escape EcoQoS background throttling
$p.ProcessorAffinity = [IntPtr]1   # pin to CPU0 (a P-core)
```

`PriorityClass=High` matters — the first attempt on 2026-08-08 used `Start-Job` and
under-stressed the CPU badly enough to be discarded.

While it runs, in the FIVR window: set CPU Core offset to **0 mV → Apply**, watch VID for
~30 s; then **−90 mV → Apply**, watch another ~30 s. Read VID off TS's own display.

**Pass condition: VID separates by roughly 80–90 mV at the same multiplier.** The locked
result was 6.7 mV. There is no ambiguous middle here.

**Fallback (if the FIVR window is still greyed):** repeat the 2026-08-08 profile A/B —
Performance (−89.8 mV) vs Game (0 mV), same pinned burst, compare VID at `MULTI ≥ 44`.
EPP is already matched at 128 across profiles 0/1/2, so that confound is controlled.

Then confirm against the log:

```bash
awk 'NR>1&&NF>=11&&$3+0>=48{v=$10+0; if(v<0.5||v>2)next; n++; a[n]=v; sm+=$3+0}
     END{for(i=1;i<n;i++)for(j=i+1;j<=n;j++)if(a[i]>a[j]){t=a[i];a[i]=a[j];a[j]=t}
     printf "n=%d avgMULTI=%.2f VIDmed=%.4f VIDp95=%.4f\n",n,sm/n,a[int(n*0.5)],a[int(n*0.95)]}' \
  /mnt/c/Users/dreux/Desktop/Logs/2026-08-24.txt
```

Baseline to beat: **VIDmed ≈ 1.40** at avgMULTI ≈ 49.6.

---

## Step 2 — Set the undervolt properly (only after Step 1 passes)

### −89.8 mV is NOT a known-good value

This is the most important correction in this document. The 2026-08-08 measurement proved
the offset **never applied**. So this chip has never actually run at −89.8 mV. "It was
stable there for months" only ever meant "it was stable at 0 mV." Treat the current .ini
value as an untested guess, not a validated setting.

### What to set

1. **Zero System Agent** if 0b showed it carrying an offset.
2. **Core and Cache matched.** On Raptor Lake, mismatched core/cache offsets are a common
   instability source. Whatever you give Core, give P-Cache.
3. **Start at −50 mV**, not −90. Step down **10 mV at a time**, validating each step.
4. Leave iGPU / iGPU Unslice alone — the dGPU does the work and there is nothing to gain.
5. E-Cache: leave at 0 until Core/Cache is settled, then tune separately if you care to.

### Validate the right way — this chip's failure mode is not the obvious one

On Raptor Lake an offset undervolt shifts the **entire** V/f curve, and the **low-frequency
end has the least margin**. Instability therefore shows up at **idle and light load**, not
in an all-core bench. Passing Cinebench proves very little here.

- All-core bench (TS Bench / Cinebench) — necessary, not sufficient.
- **Hours of ordinary light use and idle** — this is where an over-aggressive offset bites.
- Check for machine-check exceptions after each step:

```powershell
Get-WinEvent -FilterHashtable @{LogName='System'; Id=124,47,41} -MaxEvents 20 -EA SilentlyContinue |
  Select TimeCreated, Id, ProviderName, Message | Format-List
```

**Any WHEA 124 means back off 10 mV immediately.** A silent reboot with Event 41 and no
clean shutdown counts as a failure too.

### The degradation caveat still stands

The i9-14900HX is an affected Raptor Lake SKU. Vmin-shift degradation is real, cumulative
and permanent. Undervolting *reduces* that stress, so it is on the right side of the
ledger — but a chip that has been running at stock voltage through the whole degradation
window may have less margin than a fresh one. That is another reason to start at −50 mV
and walk down rather than jumping to −90.

**Unchanged regardless of how any of this lands: do not roll back the BIOS, and do not use
BIOS mod/unlock tools.** That trade was bad when undervolting was impossible and it is
still bad now that it isn't.

---

## Step 3 — The bigger prize: check whether Turbo Ratio Limits also unlocked

This may be worth more than the undervolt itself.

The July/August investigation concluded this machine has **no usable partial frequency cap** —
`PROCTHROTTLEMAX` a no-op, EPP not capping sustained clock, TS `Speed Shift Max` inert.
That verdict was reached *while the OC mailbox was locked*. **Turbo Ratio Limits (MSR 0x1AD)
and TVB ratio clipping are gated by the same OC lock bit that was blocking FIVR.**

**CONFIRMED UNLOCKED 2026-08-23.** `Turbo Overclocking: Unlimited`, and the Turbo Groups
panel is live and editable:

| Group | Ratio | Active cores |
|---|---|---|
| **0** | **58** | **1** |
| **1** | **58** | **2** |
| 2-7 | 52 | 3-8 |

Note what that says: **3+ active cores are already capped at 52x. Only 1-2 core boost runs
to 58x.** The spike population is 1-2 core events, so Groups 0 and 1 are precisely the knob.

You get the lever that provably did not exist before: a real
**per-active-core-count ratio cap**. Capping 1–2 active cores at ~50–52x instead of 58x
directly targets the low-power 100 °C spike population — the 5/16 hot samples at 40–55 W
and 7–11% C0%, where one P-core at ~5 GHz / ~1.37 V has enough local power density that
no fan can win the race. Those spikes are the actual daily complaint, and a ratio cap is
the one thing that addresses them at the source.

**One thing not to expect:** do not assume `Speed Shift Max` will suddenly start working.
It writes `IA32_HWP_REQUEST.Maximum` — a *different* MSR, not gated by the OC lock. Its
inertness is more likely a platform `HWP_REQUEST_PKG` override. Different mechanism; do not
bundle the two.

---

## Step 3b — PROCHOT / TCC Activation Offset: the one lever never tested

Worth calling out separately because it is **independent of everything above** and can be
tested today either way.

The five levers the July/August work tested were Speed Shift Max, `PROCTHROTTLEMAX`,
`PERFBOOSTMODE`, TS EPP, and turbo-off. **TCC Activation Offset was not among them.** It is
MSR 0x1A2 `TEMPERATURE_TARGET` — it lowers effective TjMax so thermal clipping engages
*earlier*. (Different mechanism from BD PROCHOT, which memory correctly notes is
VRM-protective and greyed out. Don't conflate them.)

Why it fits this machine's actual complaint: the problem population is single-core spikes at
**40-55 W and 7-11% C0%**. Lowering TjMax clips exactly those at the source, and costs almost
nothing at light load because you are nowhere near a sustained thermal limit there.

Current state: `PROCHOT_Offset0=0x1` on Performance (trip at TjMax-1 = 99 C), `0x3` on the
others.

**REVISED 2026-08-23, and DEPRIORITISED.** An offset of 8-10 (trip 90-92 C) was calibrated
for a machine with no other lever. That is no longer the situation: Turbo Groups are unlocked
(Step 3), and a ratio cap is far more surgical — it clips only the 1-2 core spikes, whereas
TCC offset clips *everything*, sustained multi-core work included. On this machine sustained
71 W settles at 85-89 C, so a 90 C trip would bite real workloads.

**Do Step 3 first.** If you still want TCC offset afterward, start at **3-5** (trip 95-97 C),
not 8-10 — that sits just above the normal sustained band and only catches genuine excursions.
Then read the temp ceiling out of the daily log; if the field is writable, `max TEMP` should
stop reaching 100.

**Prior evidence cuts slightly against it:** max TEMP is **100 C on every day measured**
(08-20/21/22/23) despite profile 0 already requesting offset 1, which is weak evidence the
write is not taking. But a 1 Hz sample can legitimately catch 100 before TCC clips, so that
is suggestive, not decisive. Ten minutes to settle.

## Step 4 — Fix a live regression that is costing you performance right now

```
EnPerfPref0=128     <-- Performance profile
EnPerfPref1=128
EnPerfPref2=128
EnPerfPref3=220     <-- Battery, correct
```

**EPP 128 on the Performance profile is leftover test scaffolding.** It was set on
2026-08-08 to match the Game profile as a clean control arm for the undervolt A/B. It was
never meant to be permanent. The original value was **32**.

This is not cosmetic. The 2026-08-08 Legion-modes work measured EPP-driven efficiency bias
roughly **halving median delivered clock (3.4 → 1.8 GHz)** and power (28.9 → 15.8 W) at
matched bursty load, with no PL1/PL2/EDP/TEMP reason tag — the CPU simply is not being
asked to boost. Your daily driver is sitting in that state right now.

Set Performance back to **EPP 32**. Independent of everything else in this document, and
immediately visible in the same logs.

Also leftover and worth a deliberate decision rather than drift:

- `SpeedShiftMaxMin0=0x3401` → Speed Shift **Max 52 / Min 1** on Performance. Test residue.
  It measured inert, but if Step 3 shows the OC lock is gone, re-check whether it now binds —
  a live 52x cap is a real ceiling you did not choose.
- `PROCHOT_Offset0=0x1` vs `0x3` on the other profiles. Offset 1 trips thermal throttling at
  TjMax−1 instead of TjMax−3. Make it consistent and intentional.

---

## Step 5 — The Game profile is a trap; fix it before you use it

You have never used the stock Game profile. It is currently the worst possible gaming config:

```
NoSetPL=0x6          -> bit 1 set: Game writes NO power limit, defers to the Lenovo EC's full power
FIVRVoltage01=0x00000000, UnlockVoltage01=0   -> Game has NO undervolt
```

**Maximum power with zero voltage headroom** — precisely the PROCHOT recipe. (Its being
untouched is exactly what made it the clean control arm for the A/B, which is why it is
still in that state.)

If you build a Gaming profile on it: give it an **explicit, thermally validated PL**, and
apply the **same validated undervolt** as Performance. Do not let it run uncapped.

---

## Step 6 — Verify with the telemetry you already have

Do not add HWiNFO back. TS's daily logs are already running, already carry VID, and are the
instrument that answered this question. Re-run this after each change and compare:

```bash
cd /mnt/c/Users/dreux/Desktop/Logs
# VID at matched load + temp profile + throttle reasons, for one day
awk 'NR>1&&NF>=11{t=$7+0;n++;s+=t;if(t>=100)a++;if(t>=95)b++;
      if($3+0>=48){v=$10+0; if(v>0.5&&v<2){vn++;arr[vn]=v;sm+=$3+0}}
      if(NF>=12)for(i=12;i<=NF;i++) if($i ~ /^[A-Z]/) r[$i]++}
     END{for(i=1;i<vn;i++)for(j=i+1;j<=vn;j++)if(arr[i]>arr[j]){x=arr[i];arr[i]=arr[j];arr[j]=x}
      printf "temp: n=%d mean=%.1f  >=95C=%.2f%%  >=100C=%.2f%%\n",n,s/n,100*b/n,100*a/n;
      printf "VID@MULTI>=48: n=%d avgMULTI=%.2f med=%.4f p95=%.4f\n",vn,sm/vn,arr[int(vn*0.5)],arr[int(vn*0.95)];
      printf "reasons: "; for(k in r) printf "%s=%d ",k,r[k]; print ""}' 2026-08-24.txt
```

**Targets, against the baselines in the table above:**

| Metric | Locked baseline | Target |
|---|---|---|
| VID median @ MULTI≥48 | ~1.40 | ≤1.32 (a real −80 mV) |
| ≥100 °C samples | 0.87% (08-23) | materially lower |
| TEMP throttle reasons | 370/day (08-23) | materially lower |
| WHEA 124 | 0 | **must stay 0** |

Do not compare a light day against a heavy one — 08-21 and 08-22 look cool (0.12%, 0.08%
≥100 °C) mostly because the machine was doing less. Compare like workloads, or compare a
full week.

---

## Order of operations

1. **0a + 0b** — GUI checks. 0b (System Agent) is a safety gate.
2. **Step 4** — set Performance EPP back to 32. Costs nothing, helps immediately, independent of everything else.
3. **Step 1** — controlled A/B. Does the undervolt actually apply?
4. **Step 3** — are Turbo Ratio Limits unlocked? Possibly the biggest win available.
5. **Step 3b** — PROCHOT/TCC offset, *only if Step 3 is insufficient*, and start at 3-5 not 8-10.
5. **Step 2** — walk the undervolt down from −50 mV with real light-load validation.
6. **Step 5** — only when building the Gaming profile.

**Never hand-edit `ThrottleStop.ini` while TS is running** — `SaveOnExit=2` overwrites the
file on exit. Use the GUI, or close TS first.

---

## Memory / prior-finding status after this

- `project-fivr-undervolt-locked` — **REOPENED, pending Step 1.** Its own stated reopening
  condition (new firmware evidence) has been met by the BIOS change.
- `project-bd-prochot-throttle` — its "no voltage lever exists" and "no usable partial
  frequency cap" verdicts are **provisionally suspended** pending Steps 1 and 3. Both were
  established while the OC mailbox was locked.
- The July MSR-lock fix is **intact and unaffected**: `MSRLock=0x0`, `LockPowerLimits=0`.
  Nothing in this plan touches it. Do not re-litigate that one.

---

## Addendum 2026-08-23 — Battery profile: EPP is correct, Disable Turbo is not working

### EPP 220 on Battery is right. Do not set it to 32.

EPP is the one lever measured to work on this machine under bursty load (2026-08-08: an EPP
efficiency bias roughly halved median delivered clock and power). Battery's `EnPerfPref3=220`
is the setting doing the work. Measured over the last 10 days of logs:

| | n | avg MULTI | avg VID | avg POWER | avg TEMP |
|---|---|---|---|---|---|
| On AC | 279,109 | 17.55 | 1.2346 | 18.4 W | 61.2 C |
| On battery | 132,364 | 14.26 | **1.0065** | **15.2 W** | 61.0 C |

That VID and power gap is the Battery profile working as intended. Setting EPP to 32 there
would make the machine boost aggressively on battery and cost runtime for nothing. If battery
ever feels too sluggish, soften 220 -> ~180; do not go near 32.

(`EnPerfPref0` is now **32** — the Aug-8 leftover has been corrected. Step 4 is done.)

### "Disable Turbo" on the Battery profile is NOT taking effect

Intent is sound, but it does not work. Restricted to **sustained** battery runs (`BAT_mW != 0`
for >=300 consecutive samples, so brief AC battery-assist spikes are excluded):

```
n=132,205   avgMULTI=14.27   maxMULTI=53.18   samples >22x: 14,379 (10.88%)
```

Non-turbo ratio is **21**. If Disable Turbo were effective, MULTI could not exceed ~22.
It reaches **53.18x**, and does so in nearly 11% of sustained-battery samples.

This is the same class of finding as `PROCTHROTTLEMAX` being a no-op: TS's Disable Turbo
writes `IA32_MISC_ENABLE` bit 38, which autonomous HWP appears to ignore on this machine.

**If you actually want turbo off on battery, use the path that was already proven to work
here** — Windows `PERFBOOSTMODE=0`, measured 2026-07-17 at 2179 MHz / ~0.85 V / ~63 C / 15.7 W.
Set it on the **DC side only**:

```
powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 0
powercfg /setactive SCHEME_CURRENT
```

Caveat: Legion Toolkit rewrites this same single scheme (`381b4222-...`). LLT writes `PERFEPP`,
not `PERFBOOSTMODE`, so this should survive — but re-check it after LLT mode changes.

**Whether you want this at all is a judgement call.** Battery already averages 15.2 W and
14.26x. Turbo-off would be a third efficiency hammer stacked on EPP 220 and PL1 17 W, and it
makes the machine genuinely slow (2.2 GHz ceiling). The occasional turbo burst is arguably
what keeps it usable. Only reach for it if you want maximum runtime.

### SpeedStep unchecked on Battery — inert, ignore it

Legacy EIST (`IA32_MISC_ENABLE` bit 16) is superseded by Speed Shift / HWP, which is active
here (`SpeedShift=1`, and every July finding shows HWP is the operative path). It makes no
practical difference on this machine. Also, per the .ini `EIST=6` appears to set only profiles
1 and 2, which would mean **Performance has it unchecked too** — Battery may not be the odd
one out at all. Not worth chasing either way.

### Still-inert: Speed Shift Max

`SpeedShiftMaxMin0=0x3401` sets Performance's Speed Shift **Max = 52**, yet the logs show
**maxMULTI 57.18 on AC**. So it is still not binding, even post-OC-unlock — consistent with the
July verdict and with it writing a different (non-OC-gated) MSR. **Do not rely on it as a
frequency cap.** Use the Turbo Groups (Step 3), which are the real thing.

---

## Addendum 2 — 2026-08-23 22:55: settings applied, verification burst

### Applied state (decoded from .ini, mtime 22:53)

| | Performance (p0) | Battery (p3) |
|---|---|---|
| Turbo Groups 1-8 core | 54/54/52/52/52/52/52/52 | 54/54/52/52/52/52/52/**50** |
| CPU Core offset | **-90.82 mV** | **-90.82 mV** |
| CPU P Cache offset | **-89.84 mV** | -90.82 mV |
| CPU E Cache offset | **-89.84 mV** | **-45.90 mV** |
| EPP | 32 | **200** |
| TVB | on | **off** |
| V-Max Stress | **off** | on |

Turbo Group ratios decode from `OneAD_EAX0=0x34343636 / EDX0=0x34343434` (bytes = 0x36,0x36,
0x34,0x34 -> 54,54,52,52 for 1-4 cores) and `EAX3/EDX3=0x32343434` giving Battery's 50 at 8 cores.
Game/Internet are untouched at `0x34343A3A` = 58/58/52/52.

**Plane offsets are no longer matched.** Core moved to -90.8 but P Cache and E Cache still hold
-89.8 on Performance, and Battery's E Cache sits at **-45.9 mV**. The 1 mV Core/P-Cache gap is
harmless; the 45 mV E Cache gap on Battery is almost certainly leftover, not intent. Simplest
correct state: **same offset on Core, P Cache and E Cache, on both profiles.**

**Battery EPP is 200** (softened from 220, not set to 32) — correct, keep it.

**TVB / V-Max Stress asymmetry is pre-existing**, not something introduced today:
`TVBoost=0x7` (bits 0,1,2 -> profiles 0,1,2 on; Battery off) and `VMaxStress=0x8`
(bit 3 -> Battery only). Worth making deliberate rather than inherited.

### Consequence of Disable Turbo being inert: Battery's Turbo Groups ARE the battery cap

Since TS's Disable Turbo does not take effect (Addendum 1), Battery's Turbo Groups are what
actually limits boost on DC. That makes them the working lever the checkbox pretended to be.
If you want less boost on battery, lower Groups 0/1 there — that path is proven to apply.

### Verification burst, 22:55:31-22:55:51 (20 s, single thread, High priority, pinned CPU0)

```
n=28   maxMULTI=51.32   maxVID=1.4507   maxTEMP=100
```

- **No sample exceeded the 54x cap** (peak 51.32). But nothing *tried* to exceed it either, so
  this is consistent with the cap working, **not proof of it**. Earlier today on AC the log hit
  **57.18x**, so a heavier 1-2 core burst is the real test.
- **VID during the hot samples ran 1.30-1.34** (e.g. 1.3153 at the 100 C sample) against
  ~1.40-1.43 for comparable pre-undervolt samples. The undervolt is visibly holding.
- **The 100 C spike still happened** — 22:55:46, TEMP 100 at **44.0 W and 9.0% C0%**.

### The uncomfortable part: the ratio cap cannot fix these spikes

The 100 C sample occurred at **MULTI 47.72 — well below the 54x cap.** A cap at 54 is simply
never reached by these events, so it cannot clip them. This restates the July physics note:
~5.0 GHz still sits at 1.33-1.44 V, and **real relief needs a mid-40s multiplier or below.**

Caveats before over-reading this: `MULTI` is a 1 s average across cores, so the instantaneous
peak core may have been higher; the machine was heat-soaked from repeated 97-99 C samples
seconds earlier; and 20 s is far too short for fans to ramp, which July established is when
these events land.

**Do not chase this with more 20-second bursts.** The right measurement is a full ordinary day
compared against the baselines already recorded (08-20: 0.89% of samples >=100 C; 08-23: 0.87%).
Run tomorrow normally, then re-run the Step 6 awk and compare like-for-like.

---

## Addendum 3 — 2026-08-23 23:03: offsets synced, V-Max Stress off

**Offsets confirmed synced.** All six keys — `FIVRVoltage00/03/20/23/50/53` (CPU Core,
CPU P Cache, CPU E Cache x Performance, Battery) — now read `0xF4600000` = **-90.82 mV**, all
with `UnlockVoltage=1`. Core, P Cache and E Cache matched on both profiles. This is the
correct configuration.

`VMaxStress=0x0` — off on every profile. `TVBoost=0x7` unchanged (still off on Battery only).

### V-Max Stress: unchecking is right, and the logs confirm the reasoning

The advice to let microcode handle it is sound, and it is now measured rather than asserted.
`VMaxStress` was `0x8` — bit 3, i.e. **Battery profile only**. Over the last 10 days:

| | samples | VMAX throttle events |
|---|---|---|
| On AC (checkbox **off**) | 32,463 | **252** |
| On battery (checkbox **on**) | 30,824 | **1** |

VMAX throttling fires almost exclusively on AC, where the checkbox was *not* set. So the
voltage ceiling is being enforced by firmware/microcode independently of TS's checkbox —
which is exactly the "let the microcode handle it" case. Turning it off changes nothing the
platform was not already doing, and on a Raptor Lake part under the 0x133 Vmin-shift
mitigations, deferring to microcode is the conservative choice.

### Observation, for information only — no action indicated

Peak VID on AC across 412,490 samples is **1.5935 V**. Before reading anything into that:
every one of the top-VID samples sits at **low multiplier (16-40), low load (C0% 5-24%) and
low power (8-47 W)**.

```
2026-08-23 11:31:16  MULTI=16.17  C0%=11.8  TEMP=64  VID=1.5935  PWR=16.6
2026-08-23 21:30:50  MULTI=20.09  C0%= 9.3  TEMP=59  VID=1.5651  PWR=15.1
2026-08-20 23:07:47  MULTI=34.00  C0%= 8.3  TEMP=72  VID=1.5479  PWR=24.0
```

That is VID read from one core briefly touching its max ratio while the rest idle — the top
of the V/f curve at **low current**, which is the least damaging form. Vmin-shift degradation
is driven by high voltage *combined* with sustained current and heat, which this is not.
Across the whole set, `VID > 1.45 V` occurs in 4.16% of samples at an average of 30.4 W.

Also note the July figure of "VID max 1.5042 V" came from a **621-sample, 10-minute window**
versus **412,490 samples** here. A higher observed maximum over 660x more samples is a
sampling-size artifact, not evidence that anything changed.

**Still the right next step: use the machine normally tomorrow, then re-run the Step 6 awk and
compare against the recorded baselines.** Nothing further to tune tonight.
